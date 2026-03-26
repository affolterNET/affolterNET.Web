using affolterNET.Web.Bff.Configuration;
using Microsoft.AspNetCore.Builder;
using affolterNET.Web.Bff.Middleware;
using affolterNET.Web.Bff.Options;
using affolterNET.Web.Core.Extensions;
using affolterNET.Web.Core.Middleware;
using affolterNET.Web.Core.Models;

namespace affolterNET.Web.Bff.Extensions;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Configures the complete BFF application pipeline with proper middleware order and flexibility
    /// This method ensures correct security practices and prevents middleware ordering mistakes
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <param name="bffOptions"></param>
    public static IApplicationBuilder ConfigureBffApp(this IApplicationBuilder app, BffAppOptions bffOptions)
    {
        // 1. EXCEPTION HANDLING (Always first)
        if (bffOptions.IsDev)
        {
            app.UseDeveloperExceptionPage();
            // Note: WebAssembly debugging should be configured in the main application
        }
        else
        {
            app.UseExceptionHandler(bffOptions.Bff.ErrorPath);
        }

        // 2. SECURITY HEADERS (Always second - protects ALL responses)
        if (bffOptions.EnableSecurityHeaders)
        {
            app.UseMiddleware<SecurityHeadersMiddleware>();
        }

        // 2.5. REQUEST LOGGING (opt-in, skips excluded paths like /health/)
        if (bffOptions.RequestLogging.Enabled)
        {
            app.UseMiddleware<RequestLoggingMiddleware>();
        }

        // 3. HTTPS REDIRECTION
        if (bffOptions.Bff.EnableHttpsRedirection)
        {
            app.UseHttpsRedirection();
        }

        // 3.5. WEBSOCKETS (Required for YARP to proxy WebSocket connections, e.g., Vite HMR)
        if (bffOptions.IsDev)
        {
            app.UseWebSockets();
        }

        // 4. STATIC FILES
        if (bffOptions.Bff.EnableStaticFiles)
        {
            app.UseStaticFiles();
        }

        // 5. API DOCUMENTATION (Swagger/OpenAPI) - After security, before routing
        if (bffOptions.Swagger.EnableSwagger)
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json",
                    $"{bffOptions.Swagger.Title} {bffOptions.Swagger.Version}");
                
                // Note: No OAuth configuration needed - using BFF cookie authentication
                // Users should log into the main application first, then Swagger will work automatically
            });
        }

        // 6. ROUTING (Required before auth)
        app.UseRouting();
        
        // 7. CUSTOM MIDDLEWARE (After routing)
        bffOptions.ConfigureAfterRoutingCustomMiddleware?.Invoke(app);

        // 8. CORS (Must be after UseRouting and before UseAuthentication)
        if (bffOptions.Cors.Enabled)
        {
            // Validate CORS configuration at startup
            bffOptions.Cors.Validate(bffOptions.IsDev);
            app.UseCors();
        }

        // 9. ANTIFORGERY (Always enabled for CSRF protection, regardless of auth mode)
        if (bffOptions.Bff.EnableAntiforgery)
        {
            app.UseAntiforgery();
        }

        // 10. AUTHENTICATION & AUTHORIZATION PIPELINE
        if (bffOptions.Bff.AuthMode != AuthenticationMode.None)
        {
            app.UseAuthentication();

            if (bffOptions.Bff.EnableTokenRefresh)
            {
                app.UseMiddleware<RefreshTokenMiddleware>();
            }

            if (bffOptions.Bff is { AuthMode: AuthenticationMode.Authorize, EnableRptTokens: true })
            {
                app.UseMiddleware<RptMiddleware>();
            }

            // Custom authorization handling for API routes
            if (bffOptions.Bff.EnableNoUnauthorizedRedirect)
            {
                app.UseMiddleware<NoUnauthorizedRedirectMiddleware>((object)bffOptions.Bff.ApiRoutePrefixes);
            }

            app.UseAuthorization();
        }

        // 11. ANTIFORGERY TOKEN MIDDLEWARE (After authorization)
        if (bffOptions.Bff.EnableAntiforgery)
        {
            app.UseMiddleware<AntiforgeryTokenMiddleware>();
        }

        // 12. CUSTOM MIDDLEWARE (Before endpoint mapping)
        bffOptions.ConfigureBeforeEndpointsCustomMiddleware?.Invoke(app);

        // 13. API 404 handling (Before endpoint mapping)
        if (bffOptions.Bff.EnableApiNotFound)
        {
            app.UseMiddleware<ApiNotFoundMiddleware>((object)bffOptions.Bff.ApiRoutePrefixes);
        }

        // 14. ENDPOINT MAPPING
        app.UseEndpoints(endpoints =>
        {
            if (bffOptions.Cloud.MapHealthChecks)
            {
                endpoints.MapStandardHealthChecks();
            }
            endpoints.MapRazorPages();
            endpoints.MapControllers();

            // YARP Reverse Proxy
            endpoints.MapReverseProxy();

            // Fallback to main page
            if (!string.IsNullOrEmpty(bffOptions.Bff.FallbackPage))
            {
                endpoints.MapFallbackToPage(bffOptions.Bff.FallbackPage);
            }
        });

        return app;
    }
}