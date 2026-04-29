using System.Reflection;
using affolterNET.Web.Bff.Configuration;
using affolterNET.Web.Bff.Middleware;
using affolterNET.Web.Bff.Options;
using affolterNET.Web.Bff.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using affolterNET.Web.Core.Extensions;
using affolterNET.Web.Core.Models;
using affolterNET.Web.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

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
            .AddAuthorizationPolicies(bffOptions.ConfigureAuthorizationPolicies);

        // Swagger
        services.AddSwagger(bffOptions);

        // CORS
        services.AddCors(bffOptions.Cors);

        // Add BFF-specific authentication setup
        services.AddBffAuthenticationInternal(bffOptions);

        // Add Data Protection key persistence
        services.AddAzureBlobDataProtection(bffOptions.DataProtection);

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
        var authBuilder = services.AddAuthentication(options =>
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
                            // Forward extra OIDC parameters from AuthenticationProperties
                            foreach (var param in context.Properties.Parameters)
                            {
                                if (param.Value is string value && !string.IsNullOrEmpty(value))
                                {
                                    context.ProtocolMessage.SetParameter(param.Key, value);
                                }
                            }

                            // For signup: redirect to Keycloak registration endpoint
                            // This works reliably with PAR unlike prompt=create
                            if (path.Equals("/bff/account/signup", StringComparison.OrdinalIgnoreCase))
                            {
                                context.ProtocolMessage.IssuerAddress =
                                    context.ProtocolMessage.IssuerAddress.Replace(
                                        "/protocol/openid-connect/auth",
                                        "/protocol/openid-connect/registrations");
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

        // Conditionally add JWT Bearer as secondary auth scheme
        if (bffOptions.JwtBearer.Enabled)
        {
            authBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = bffOptions.AuthProvider.Authority;
                options.RequireHttpsMetadata = bffOptions.JwtBearer.RequireHttpsMetadata;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = bffOptions.JwtBearer.ValidateAudience,
                    ValidAudiences = bffOptions.JwtBearer.ValidAudiences.Length > 0
                        ? bffOptions.JwtBearer.ValidAudiences
                        : [bffOptions.AuthProvider.ClientId],
                    ValidateIssuer = bffOptions.JwtBearer.ValidateIssuer,
                    ValidIssuers = bffOptions.JwtBearer.ValidIssuers.Length > 0
                        ? bffOptions.JwtBearer.ValidIssuers
                        : null,
                    ValidateLifetime = bffOptions.JwtBearer.ValidateLifetime,
                    ValidateIssuerSigningKey = bffOptions.JwtBearer.ValidateIssuerSigningKey,
                    RoleClaimType = bffOptions.JwtBearer.RoleClaimType,
                    ClockSkew = bffOptions.JwtBearer.ClockSkew,
                };

                // azp validation for Keycloak client credentials tokens
                var validAzps = bffOptions.JwtBearer.ValidAuthorizedParties;
                if (validAzps.Length > 0)
                {
                    options.TokenValidationParameters.ValidateAudience = false;
                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = context =>
                        {
                            var azp = context.Principal?.FindFirst("azp")?.Value;
                            if (azp == null || !validAzps.Contains(azp))
                            {
                                context.Fail("Invalid authorized party (azp)");
                            }
                            return Task.CompletedTask;
                        }
                    };
                }
            });
        }

        // Allow consumer to register additional auth schemes (e.g., BasicAuth)
        bffOptions.ConfigureAdditionalAuthSchemes?.Invoke(authBuilder);

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
}