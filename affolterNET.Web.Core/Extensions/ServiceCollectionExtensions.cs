using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using affolterNET.Web.Core.Configuration;
using affolterNET.Web.Core.Authorization;
using affolterNET.Web.Core.HealthChecks;
using affolterNET.Web.Core.Options;
using affolterNET.Web.Core.Services;
using affolterNET.Web.Core.Swagger;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NETCore.Keycloak.Client.HttpClients.Abstraction;
using NETCore.Keycloak.Client.HttpClients.Implementation;

namespace affolterNET.Web.Core.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds core infrastructure services required for authentication (memory cache, HTTP context accessor)
    /// </summary>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // Add memory cache if not already added
        services.AddMemoryCache();

        // Add HTTP context accessor if not already added
        services.AddHttpContextAccessor();

        return services;
    }

    /// <summary>
    /// Adds CORS configuration using ASP.NET Core's built-in CORS with affolterNET CorsOptions
    /// </summary>
    public static IServiceCollection AddCors(this IServiceCollection services,
        AffolterNetCorsOptions corsOptions)
    {
        if (corsOptions?.Enabled == true)
        {
            services.AddMemoryCache();
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy => { ConfigureCorsPolicy(policy, corsOptions); });
            });
        }

        return services;
    }

    /// <summary>
    /// Adds Keycloak client integration and configuration
    /// </summary>
    public static IServiceCollection AddKeycloakIntegration(this IServiceCollection services,
        CoreAppOptions coreAppOptions)
    {
        // Register Keycloak client
        services.AddSingleton<IKeycloakClient>(_ => new KeycloakClient(coreAppOptions.AuthProvider.AuthorityBase));

        return services;
    }

    /// <summary>
    /// Adds RPT (Request Party Token) services for permission management
    /// </summary>
    public static IServiceCollection AddRptServices(this IServiceCollection services)
    {
        // Register RPT and token services
        services.AddScoped<TokenHelper>();
        services.AddScoped<RptTokenService>();
        services.AddSingleton<RptCacheService>();
        services.AddScoped<IPermissionService, PermissionService>();

        return services;
    }

    /// <summary>
    /// Adds authorization policies and custom authorization handlers
    /// </summary>
    public static IServiceCollection AddAuthorizationPolicies(
        this IServiceCollection services,
        Action<AuthorizationOptions>? configurePolicies = null)
    {
        services.AddAuthorization(options =>
        {
            configurePolicies?.Invoke(options);
        });

        services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }

    /// <summary>
    /// Adds security headers configuration and middleware services
    /// </summary>
    public static IServiceCollection AddSecurityHeaders(this IServiceCollection services)
    {
        // services.Configure<AuthConfiguration>(config => { config.SecurityHeaders = authConfig.SecurityHeaders; });

        return services;
    }

    public static IServiceCollection AddSwagger(this IServiceCollection services, CoreAppOptions coreOptions)
    {
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc(coreOptions.Swagger.Version,
                new() { Title = coreOptions.Swagger.Title, Version = coreOptions.Swagger.Version });

            // Add XML comments if available
            var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
            if (File.Exists(xmlPath))
            {
                c.IncludeXmlComments(xmlPath);
            }
            
            // Document health check endpoints manually
            if (coreOptions.Cloud.MapHealthChecks && coreOptions.Swagger.ShowHealthEndpoints)
            {
                c.DocumentFilter<HealthCheckDocumentFilter>();
            }
        });
        return services;
    }

    /// <summary>
    /// Configures ASP.NET Core CORS policy from affolterNET CorsOptions
    /// </summary>
    private static void ConfigureCorsPolicy(CorsPolicyBuilder policy,
        Configuration.AffolterNetCorsOptions affolterNetCorsOptions)
    {
        // Origins
        if (affolterNetCorsOptions.AllowedOrigins.Contains("*"))
        {
            policy.AllowAnyOrigin();
        }
        else if (affolterNetCorsOptions.AllowedOrigins.Count > 0)
        {
            policy.WithOrigins(affolterNetCorsOptions.AllowedOrigins.ToArray());
        }

        // Methods
        if (affolterNetCorsOptions.AllowedMethods.Contains("*"))
        {
            policy.AllowAnyMethod();
        }
        else if (affolterNetCorsOptions.AllowedMethods.Count > 0)
        {
            policy.WithMethods(affolterNetCorsOptions.AllowedMethods.ToArray());
        }

        // Headers
        if (affolterNetCorsOptions.AllowedHeaders.Contains("*"))
        {
            policy.AllowAnyHeader();
        }
        else if (affolterNetCorsOptions.AllowedHeaders.Count > 0)
        {
            policy.WithHeaders(affolterNetCorsOptions.AllowedHeaders.ToArray());
        }

        // Credentials
        if (affolterNetCorsOptions.AllowCredentials)
        {
            policy.AllowCredentials();
        }

        // Exposed Headers
        if (affolterNetCorsOptions.ExposedHeaders.Count > 0)
        {
            policy.WithExposedHeaders(affolterNetCorsOptions.ExposedHeaders.ToArray());
        }

        // Max Age
        if (affolterNetCorsOptions.MaxAge > 0)
        {
            policy.SetPreflightMaxAge(TimeSpan.FromSeconds(affolterNetCorsOptions.MaxAge));
        }
    }

    public static IServiceCollection AddStandardHealthChecks(this IServiceCollection services,
        string? keycloakBaseUrl = null)
    {
        // Startup status + marker
        services.AddSingleton<StartupStatusService>();
        services.AddHostedService<StartupMarker>();

        // HttpClient for Keycloak
        if (!string.IsNullOrEmpty(keycloakBaseUrl))
        {
            services.AddHttpClient("keycloak", client =>
            {
                client.BaseAddress = new Uri(keycloakBaseUrl.TrimEnd('/'));
                client.Timeout = TimeSpan.FromSeconds(5);
            });
        }

        // Health checks
        var health = services.AddHealthChecks()
            .AddCheck<StartupHealthCheck>("startup", tags: new[] { "startup" })
            .AddCheck("self", () => HealthCheckResult.Healthy("OK"));

        // keycloak health check only if URL provided
        if (!string.IsNullOrEmpty(keycloakBaseUrl))
        {
            health.AddCheck<KeycloakHealthCheck>("keycloak", tags: new[] { "ready", "startup" });
        }

        return services;
    }
}