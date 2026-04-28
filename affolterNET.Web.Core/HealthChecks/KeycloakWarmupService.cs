using affolterNET.Web.Core.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace affolterNET.Web.Core.HealthChecks;

/// <summary>
/// Fires a fire-and-forget request to Keycloak's <c>.well-known/openid-configuration</c>
/// endpoint shortly after app start. Wakes the Keycloak JVM in parallel with this app's
/// boot so the first user-driven auth flow doesn't pay the cold-start cost.
/// Decoupled from the readiness/startup probes so a slow or unreachable Keycloak does
/// NOT take this container out of rotation.
/// </summary>
public class KeycloakWarmupService(
    IHttpClientFactory http,
    IOptions<AuthProviderOptions> authProviderOptions,
    ILogger<KeycloakWarmupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var realm = authProviderOptions.Value.Realm;
        if (string.IsNullOrEmpty(realm))
        {
            return;
        }

        var client = http.CreateClient("keycloak");
        var url = $"realms/{realm}/.well-known/openid-configuration";

        for (var attempt = 1; attempt <= 3 && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                using var res = await client.GetAsync(url, stoppingToken);
                if (res.IsSuccessStatusCode)
                {
                    logger.LogInformation(
                        "Keycloak warm-up succeeded on attempt {Attempt}", attempt);
                    return;
                }
                logger.LogDebug(
                    "Keycloak warm-up attempt {Attempt} returned {Status}",
                    attempt, (int)res.StatusCode);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogDebug(ex, "Keycloak warm-up attempt {Attempt} failed", attempt);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
