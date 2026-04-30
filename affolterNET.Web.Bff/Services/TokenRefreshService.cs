using System.Globalization;
using affolterNET.Web.Core.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NETCore.Keycloak.Client.HttpClients.Abstraction;
using NETCore.Keycloak.Client.Models.Auth;
using NETCore.Keycloak.Client.Models.Tokens;

namespace affolterNET.Web.Bff.Services;

/// <summary>
/// Service for refreshing authentication tokens using Keycloak refresh tokens
/// This service is BFF-specific as it handles browser session cookies and token persistence
/// </summary>
public class TokenRefreshService(
    IOptionsMonitor<AuthProviderOptions> authProviderOptions,
    IKeycloakClient keycloakClient,
    ILogger<TokenRefreshService> logger)
{
    private readonly string _realm = authProviderOptions.CurrentValue.Realm;
    private readonly KcClientCredentials _clientCredentials = new()
    {
        ClientId = authProviderOptions.CurrentValue.ClientId,
        Secret = authProviderOptions.CurrentValue.ClientSecret
    };
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);

    /// <summary>
    /// Refreshes the authentication tokens if they are expired or about to expire
    /// </summary>
    /// <param name="httpContext">The current HTTP context</param>
    /// <returns>True if tokens were refreshed successfully, false otherwise</returns>
    public async Task<bool> RefreshTokensAsync(HttpContext httpContext)
    {
        await RefreshLock.WaitAsync();
        try
        {
            // Check again after acquiring the lock
            if (!await IsExpired(httpContext))
            {
                return true;
            }

            // Request new tokens from Keycloak
            logger.LogDebug("Refreshing tokens...");
            var refreshToken = await GetRefreshToken(httpContext);
            if (refreshToken == null)
            {
                logger.LogWarning("Refreshing token failed - no refresh token available");
                return false;
            }
            logger.LogDebug("Refresh token received from context");
            
            var newTokens = await RequestNewTokensAsync(refreshToken);
            if (newTokens == null)
            {
                logger.LogDebug("Requesting new tokens failed");
                return false;
            }

            // Extract new tokens from Keycloak response
            var newAccessToken = newTokens.AccessToken;
            var newRefreshToken = newTokens.RefreshToken;
            var expiresIn = newTokens.ExpiresIn;

            // Calculate new absolute expiration time
            var newExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn);
            
            // Update the tokens
            return await UpdateAuthTokensAsync(httpContext, newAccessToken, newRefreshToken, newExpiresAt);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Refreshing tokens failed");
            return false;
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    /// <summary>
    /// Gets the expiration time of the current access token
    /// </summary>
    /// <param name="httpContext">The current HTTP context</param>
    /// <returns>The expiration time or null if not available</returns>
    public async Task<DateTime?> ExpiresAt(HttpContext httpContext)
    {
        if (httpContext == null)
        {
            logger.LogError("HttpContext is null");
            return null;
        }

        var expiresAtString = await httpContext.GetTokenAsync("expires_at");
        if (!DateTime.TryParse(expiresAtString, null, DateTimeStyles.AdjustToUniversal, out DateTime expiresAt))
        {
            return null;
        }

        return expiresAt;
    }

    /// <summary>
    /// Checks if the current access token is expired or about to expire
    /// </summary>
    /// <param name="httpContext">The current HTTP context</param>
    /// <param name="secondsBeforeExpiration">Number of seconds before expiration to consider the token expired</param>
    /// <returns>True if the token is expired or about to expire</returns>
    public async Task<bool> IsExpired(HttpContext httpContext, int secondsBeforeExpiration = 10)
    {
        var expiresAt = await ExpiresAt(httpContext);
        if (expiresAt == null)
        {
            return true;
        }

        var nowUtc = DateTime.UtcNow;
        var isExpired = expiresAt <= nowUtc ||
                        expiresAt.Value.Subtract(nowUtc) < TimeSpan.FromSeconds(secondsBeforeExpiration);
        return isExpired;
    }

    private async Task<string?> GetRefreshToken(HttpContext httpContext)
    {
        if (httpContext == null)
        {
            logger.LogWarning("HttpContext is null");
            return null;
        }

        var refreshToken = await httpContext.GetTokenAsync("refresh_token");
        return refreshToken;
    }

    private async Task<KcIdentityProviderToken?> RequestNewTokensAsync(string refreshToken)
    {
        // Send refresh token request to Keycloak
        logger.LogDebug("Refreshing tokens...");
        var kcResponse = await keycloakClient.Auth.RefreshAccessTokenAsync(_realm, _clientCredentials, refreshToken);
        if (kcResponse.IsError)
        {
            // invalid_grant ("Session not active" / "Token is not active" / "Refresh token expired")
            // is the routine outcome when a user's Keycloak session has been idle past
            // SSO Session Idle / Refresh Token Lifespan. The middleware handles it correctly
            // (clear cookie → 401 → SPA redirects to login); logging at Error here would
            // trigger alerts for every routine session expiry.
            var msg = kcResponse.ErrorMessage ?? string.Empty;
            if (msg.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("Refresh token rejected by Keycloak (session likely expired): {Error}", msg);
            }
            else
            {
                logger.LogError("Refreshing tokens failed: {Error}", msg);
            }
            return null;
        }

        return kcResponse.Response;
    }

    private async Task<bool> UpdateAuthTokensAsync(HttpContext httpContext, string newAccessToken, string newRefreshToken,
        DateTime newExpiresAt)
    {
        // Get current auth ticket (cookie) and update the token values
        if (httpContext == null)
        {
            return false;
        }
        var context = httpContext;
        var currentAuth =
            await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = currentAuth.Properties;
        if (authProperties == null)
        {
            logger.LogError("Authentication properties are missing");
            throw new InvalidOperationException("Authentication properties are missing");
        }

        authProperties.UpdateTokenValue("access_token", newAccessToken);
        authProperties.UpdateTokenValue("refresh_token", newRefreshToken);
        authProperties.UpdateTokenValue("expires_at", newExpiresAt.ToString("o", CultureInfo.InvariantCulture));

        // Re-issue the authentication cookie with the new tokens
        if (currentAuth.Principal == null)
        {
            logger.LogError("Authentication principal is missing");
            throw new InvalidOperationException("Authentication principal is missing");
        }

        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            currentAuth.Principal, authProperties);
        logger.LogDebug("Tokens refreshed and saved");
        return true;
    }
}