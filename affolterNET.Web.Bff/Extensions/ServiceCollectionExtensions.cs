using System.Reflection;
using affolterNET.Web.Bff.Configuration;
using affolterNET.Web.Bff.Middleware;
using affolterNET.Web.Bff.Options;
using affolterNET.Web.Bff.Services;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using affolterNET.Web.Core.Extensions;
using affolterNET.Web.Core.Models;
using affolterNET.Web.Core.Services;
using Microsoft.Extensions.Logging;

namespace affolterNET.Web.Bff.Extensions;

public static class ServiceCollectionExtensions
{
    private static ILogger? _logger;

    /// <summary>
    /// Adds complete BFF authentication with all required services and middleware
    /// This is the single public entry point for BFF authentication
    /// </summary>
    public static BffAppOptions AddBffServices(this IServiceCollection services, AppSettings appSettings,
        IConfiguration configuration,
        Action<BffAppOptions>? configureOptions = null)
    {
        _logger = services.BuildServiceProvider().GetRequiredService<ILoggerFactory>()
            .CreateLogger(Assembly.GetEntryAssembly()?.GetName().Name!);

        // 1. Create BffAppOptions instance with constructor defaults
        var bffOptions = new BffAppOptions(appSettings, configuration);
        configureOptions?.Invoke(bffOptions);
        bffOptions.Configure(services);

        // Add core authentication services
        services.AddCoreServices()
            .AddKeycloakIntegration(bffOptions)
            .AddRptServices()
            .AddAuthorizationPolicies();

        // Swagger
        services.AddSwagger(bffOptions);

        // CORS
        services.AddCors(bffOptions.Cors);

        // Add BFF-specific authentication setup
        services.AddBffAuthenticationInternal(bffOptions);

        // Add Data Protection key persistence
        services.AddDataProtectionInternal(bffOptions);

        // Add BFF supporting services
        services.AddAntiforgeryServicesInternal(bffOptions.AntiForgery);
        services.AddReverseProxyInternal(configuration);
        return bffOptions;
    }

    /// <summary>
    /// Adds BFF authentication configuration (cookies, OIDC)
    /// </summary>
    private static IServiceCollection AddBffAuthenticationInternal(this IServiceCollection services,
        BffAppOptions bffOptions)
    {
        // add health checks
        services.AddStandardHealthChecks(bffOptions.AuthProvider.AuthorityBase);
        
        // Add authentication
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                options.DefaultSignOutScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.Cookie.Name = bffOptions.CookieAuth.Name;
                options.Cookie.HttpOnly = bffOptions.CookieAuth.HttpOnly;
                options.Cookie.SecurePolicy = bffOptions.CookieAuth.Secure
                    ? Microsoft.AspNetCore.Http.CookieSecurePolicy.Always
                    : Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = bffOptions.CookieAuth.SameSite switch
                {
                    "Strict" => Microsoft.AspNetCore.Http.SameSiteMode.Strict,
                    "Lax" => Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                    "None" => Microsoft.AspNetCore.Http.SameSiteMode.None,
                    _ => Microsoft.AspNetCore.Http.SameSiteMode.Strict
                };
                options.ExpireTimeSpan = bffOptions.CookieAuth.ExpireTimeSpan;
                options.SlidingExpiration = bffOptions.CookieAuth.SlidingExpiration;
                options.LoginPath = "/bff/account/login";
                options.LogoutPath = "/bff/account/logout";
                options.AccessDeniedPath = "/bff/access-denied";

                // BFF pattern: Return 403 JSON for API routes instead of redirecting to access denied page
                options.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToAccessDenied = context =>
                    {
                        var path = context.Request.Path.Value ?? string.Empty;

                        // Check if this is an API route
                        if (bffOptions.Bff.ApiRoutePrefixes.Any(prefix =>
                            path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                        {
                            context.Response.StatusCode = 403;
                            context.Response.ContentType = "application/json";
                            return context.Response.WriteAsJsonAsync(new
                            {
                                error = "Forbidden",
                                message = "You do not have permission to access this resource",
                                statusCode = 403,
                                path,
                                timestamp = DateTimeOffset.UtcNow
                            });
                        }

                        // For non-API routes, allow the redirect
                        context.Response.Redirect(context.RedirectUri);
                        return Task.CompletedTask;
                    },
                    OnRedirectToLogin = context =>
                    {
                        var path = context.Request.Path.Value ?? string.Empty;

                        // Check if this is an API route
                        if (bffOptions.Bff.ApiRoutePrefixes.Any(prefix =>
                            path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                        {
                            context.Response.StatusCode = 401;
                            context.Response.ContentType = "application/json";
                            return context.Response.WriteAsJsonAsync(new
                            {
                                error = "Unauthorized",
                                message = "Authentication required",
                                statusCode = 401,
                                path,
                                timestamp = DateTimeOffset.UtcNow
                            });
                        }

                        // For non-API routes, allow the redirect
                        context.Response.Redirect(context.RedirectUri);
                        return Task.CompletedTask;
                    }
                };
            })
            .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.Authority = bffOptions.AuthProvider.Authority;
                options.ClientId = bffOptions.AuthProvider.ClientId;
                options.ClientSecret = bffOptions.AuthProvider.ClientSecret;
                options.ResponseType = bffOptions.Oidc.ResponseType;
                options.SaveTokens = bffOptions.Oidc.SaveTokens;
                options.UsePkce = bffOptions.Oidc.UsePkce;

                options.Scope.Clear();
                foreach (var scope in bffOptions.Oidc.GetScopes().Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    options.Scope.Add(scope);
                }

                options.CallbackPath = bffOptions.BffAuth.RedirectUri;
                options.SignedOutCallbackPath = bffOptions.BffAuth.PostLogoutRedirectUri;
                options.SignedOutRedirectUri = "/";

                // Map claims
                options.MapInboundClaims = false;
                options.GetClaimsFromUserInfoEndpoint = true;

                // Use Keycloak's "roles" claim for ASP.NET Core role authorization
                options.TokenValidationParameters.RoleClaimType = "roles";

                // BFF pattern: Prevent automatic redirects to IDP
                // When [Authorize] fails, it challenges the DefaultChallengeScheme (OIDC)
                // We intercept here and return 401
                options.Events = new OpenIdConnectEvents
                {
                    OnRedirectToIdentityProvider = context =>
                    {
                        var path = context.Request.Path.Value ?? string.Empty;

                        // Allow redirect for explicit login and signup endpoints
                        if (path.Equals("/bff/account/login", StringComparison.OrdinalIgnoreCase) ||
                            path.Equals("/bff/account/signup", StringComparison.OrdinalIgnoreCase))
                        {
                            // Forward prompt parameter (e.g. "create" for signup)
                            if (context.Properties.Parameters.TryGetValue("prompt", out var promptValue))
                            {
                                context.ProtocolMessage.Prompt = promptValue?.ToString();
                            }

                            return Task.CompletedTask;
                        }

                        // BFF pattern: Always return 401 instead of redirecting to IDP
                        // The SPA handles showing login UI and calling /bff/account/login explicitly
                        context.Response.StatusCode = 401;
                        context.HandleResponse();
                        return Task.CompletedTask;
                    }
                };
            });

        // Register BFF-specific services (only services from this library)
        services.AddSingleton<TokenRefreshService>();
        services.AddScoped<IClaimsEnrichmentService, BffClaimsEnrichmentService>();
        services.AddScoped<IBffSessionService, BffSessionService>();
        services.AddHttpClient<IBffApiClient, BffApiClient>();

        return services;
    }

    /// <summary>
    /// Adds reverse proxy with authentication token forwarding
    /// </summary>
    private static IServiceCollection AddReverseProxyInternal(this IServiceCollection services,
        IConfiguration configuration, string sectionKey = "affolterNET:ReverseProxy")
    {
        // Check if reverse proxy configuration exists and if no, add a minimal config
        var reverseProxySection = configuration.GetSection(sectionKey);
        var hasReverseProxyConfig = reverseProxySection.Exists() && reverseProxySection.GetChildren().Any();
        if (!hasReverseProxyConfig)
        {
            // Add minimal default configuration
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ReverseProxy:Routes"] = "",
                ["ReverseProxy:Clusters"] = ""
            }!);
            var defaultConfig = configBuilder.Build();
            reverseProxySection = defaultConfig.GetSection(sectionKey);

            // Log a warning that reverse proxy configuration is missing
            _logger?.LogWarning("Using default empty YARP configuration");
        }

        services.AddReverseProxy()
            .LoadFromConfig(reverseProxySection)
            .AddTransforms<AuthTransform>();

        return services;
    }

    /// <summary>
    /// Adds antiforgery services configured for SPA scenarios with client-accessible tokens
    /// </summary>
    private static IServiceCollection AddAntiforgeryServicesInternal(this IServiceCollection services,
        BffAntiforgeryOptions bffAntiforgeryOptions)
    {
        services.AddAntiforgery(options =>
        {
            options.HeaderName = bffAntiforgeryOptions.ClientCookieName;
            options.Cookie.Name = bffAntiforgeryOptions.ServerCookieName;
            options.Cookie.SameSite = bffAntiforgeryOptions.SameSiteMode;
            options.Cookie.SecurePolicy = bffAntiforgeryOptions.RequireSecure
                ? Microsoft.AspNetCore.Http.CookieSecurePolicy.Always
                : Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
            options.Cookie.Path = bffAntiforgeryOptions.CookiePath;
        });

        return services;
    }

    /// <summary>
    /// Adds Data Protection key persistence to Azure Blob Storage.
    /// When disabled (default), ASP.NET Core uses ephemeral in-memory keys.
    /// </summary>
    private static IServiceCollection AddDataProtectionInternal(
        this IServiceCollection services, BffAppOptions bffOptions)
    {
        if (!bffOptions.DataProtection.Enabled)
        {
            return services;
        }

        var dp = bffOptions.DataProtection;
        var dpBuilder = services.AddDataProtection();

        if (!string.IsNullOrWhiteSpace(dp.ApplicationName))
        {
            dpBuilder.SetApplicationName(dp.ApplicationName);
        }

        if (!string.IsNullOrWhiteSpace(dp.StorageAccountName))
        {
            // Managed Identity auth
            var blobUri = new Uri(
                $"https://{dp.StorageAccountName}.blob.core.windows.net/{dp.ContainerName}/{dp.BlobName}");
            var credential = new ManagedIdentityCredential(dp.ManagedIdentityClientId);
            dpBuilder.PersistKeysToAzureBlobStorage(blobUri, credential);
        }
        else if (!string.IsNullOrWhiteSpace(dp.ConnectionString))
        {
            // Connection string auth (local docker, testing)
            var blobClient = new BlobClient(dp.ConnectionString, dp.ContainerName, dp.BlobName);
            dpBuilder.PersistKeysToAzureBlobStorage(blobClient);
        }

        return services;
    }
}