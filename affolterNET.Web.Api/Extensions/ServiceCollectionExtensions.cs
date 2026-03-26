using System.Reflection;
using affolterNET.Web.Api.Options;
using affolterNET.Web.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using affolterNET.Web.Core.Extensions;
using affolterNET.Web.Core.Models;
using affolterNET.Web.Core.Services;
using Microsoft.Extensions.Logging;

namespace affolterNET.Web.Api.Extensions;

public static class ServiceCollectionExtensions
{
    private static ILogger? _logger;

    public static ApiAppOptions AddApiServices(this IServiceCollection services,
    AppSettings appSettings, IConfiguration configuration,
    Action<ApiAppOptions>? configureOptions = null)
    {
        _logger = services.BuildServiceProvider().GetRequiredService<ILoggerFactory>()
            .CreateLogger(Assembly.GetEntryAssembly()?.GetName().Name!);
        
        // 1. Create BffAppOptions instance with constructor defaults
        var apiOptions = new ApiAppOptions(appSettings, configuration);
        configureOptions?.Invoke(apiOptions);
        apiOptions.Configure(services);
        
        // add health checks
        services.AddStandardHealthChecks(apiOptions.AuthProvider.AuthorityBase);
        
        // Add core authentication services
        services.AddCoreServices()
            .AddKeycloakIntegration(apiOptions)
            .AddRptServices()
            .AddAuthorizationPolicies();
        
        // Swagger
        services.AddSwagger(apiOptions);
        
        // CORS
        services.AddCors(apiOptions.Cors);
        
        // Add API-specific authentication setup
        services.AddApiAuthenticationInternal(apiOptions);

        return apiOptions;
    }

    /// <summary>
    /// Adds JWT Bearer authentication and API-specific services
    /// </summary>
    private static IServiceCollection AddApiAuthenticationInternal(this IServiceCollection services,
        ApiAppOptions apiOptions)
    {
        // Add JWT Bearer authentication
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = apiOptions.AuthProvider.Authority;
                options.RequireHttpsMetadata = apiOptions.ApiJwtBearer.RequireHttpsMetadata;
                options.SaveToken = apiOptions.ApiJwtBearer.SaveToken;
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = apiOptions.ApiJwtBearer.ValidateIssuer,
                    ValidateAudience = apiOptions.ApiJwtBearer.ValidateAudience,
                    ValidateLifetime = apiOptions.ApiJwtBearer.ValidateLifetime,
                    ValidateIssuerSigningKey = apiOptions.ApiJwtBearer.ValidateIssuerSigningKey,
                    ValidIssuers = apiOptions.ApiJwtBearer.ValidIssuers.Length > 0 ? apiOptions.ApiJwtBearer.ValidIssuers : null,
                    ValidAudiences = apiOptions.ApiJwtBearer.ValidAudiences.Length > 0
                        ? apiOptions.ApiJwtBearer.ValidAudiences
                        : apiOptions.ApiJwtBearer.ValidateAudience && !string.IsNullOrEmpty(apiOptions.AuthProvider.ClientId)
                            ? [apiOptions.AuthProvider.ClientId]
                            : null,
                    ClockSkew = apiOptions.ApiJwtBearer.ClockSkew,
                    RoleClaimType = "roles",
                };
            });

        // Register API-specific services (only services from this library)
        services.AddScoped<IClaimsEnrichmentService, ApiClaimsEnrichmentService>();

        return services;
    }
}