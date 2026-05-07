using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using affolterNET.Web.Core.Configuration;
using affolterNET.Web.Core.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace affolterNET.Web.Core.Services;

/// <summary>
/// Service for managing user permissions via Keycloak RPT tokens
/// </summary>
public class PermissionService(
    RptTokenService rptTokenService,
    RptCacheService rptCacheService,
    IMemoryCache cache,
    IHttpContextAccessor httpContextAccessor,
    ILogger<PermissionService> logger,
    IOptionsMonitor<PermissionCacheOptions> permissionCacheOptions)
    : IPermissionService
{
    private readonly PermissionCacheOptions _permissionCacheConfig = permissionCacheOptions.CurrentValue;

    public async Task<IReadOnlyList<Permission>> GetUserPermissionsAsync(string userId, string accessToken, CancellationToken cancellationToken = default)
    {
        // Master gate: when RPT/permissions are turned off via PermissionCacheOptions.Enabled
        // (forwarded from BffOptions.EnableRptTokens / ApiAuthOptions.EnableRptTokens),
        // skip the Keycloak round-trip entirely. Avoids the
        // "Client does not support permissions" warning on every login for consumers
        // that use role-based-only authorization.
        if (!_permissionCacheConfig.Enabled)
        {
            return Array.Empty<Permission>();
        }

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(accessToken))
        {
            return Array.Empty<Permission>();
        }

        // Check cache first
        var cacheKey = $"permissions:{userId}";
        if (cache.TryGetValue(cacheKey, out List<Permission>? cachedPermissions) && cachedPermissions != null)
        {
            logger.LogDebug("Retrieved permissions for user {UserId} from cache", userId);
            return cachedPermissions;
        }

        try
        {
            // Get RPT token from Keycloak
            var rptToken = await rptTokenService.GetRptTokenAsync(accessToken);
            if (rptToken == null)
            {
                logger.LogWarning("Failed to get RPT token for user {UserId}", userId);
                return Array.Empty<Permission>();
            }

            // Store in RPT cache and get decoded token
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(rptToken.AccessToken))
                return Array.Empty<Permission>();
            var decodedToken = handler.ReadJwtToken(rptToken.AccessToken);
            rptCacheService.StoreRpt(userId, rptToken, decodedToken);
            
            // Extract permissions from RPT token
            var permissions = ExtractPermissionsFromRpt(decodedToken);
            
            // Cache the permissions
            var permissionsList = permissions.ToList();
            cache.Set(cacheKey, permissionsList, _permissionCacheConfig.PermissionCacheExpiration);
            
            logger.LogDebug("Retrieved {PermissionCount} permissions for user {UserId}", permissionsList.Count, userId);
            return permissionsList;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving permissions for user {UserId}", userId);
            return Array.Empty<Permission>();
        }
    }

    public async Task<bool> HasPermissionAsync(string userId, string resource, string action, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(resource) || string.IsNullOrEmpty(action))
        {
            return false;
        }

        try
        {
            // Get access token from current HTTP context
            var accessToken = await GetAccessTokenFromContext();
            if (string.IsNullOrEmpty(accessToken))
            {
                logger.LogWarning("No access token available for permission check for user {UserId}", userId);
                return false;
            }

            var permissions = await GetUserPermissionsAsync(userId, accessToken, cancellationToken);
            
            // Check if user has the specific permission
            var hasPermission = permissions.Any(p => 
                string.Equals(p.Resource, resource, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.Action, action, StringComparison.OrdinalIgnoreCase));

            logger.LogDebug("Permission check for user {UserId}, resource {Resource}, action {Action}: {HasPermission}", 
                userId, resource, action, hasPermission);

            return hasPermission;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking permission for user {UserId}, resource {Resource}, action {Action}", 
                userId, resource, action);
            return false;
        }
    }

    public Task InvalidateUserPermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return Task.CompletedTask;
        }

        try
        {
            var cacheKey = $"permissions:{userId}";
            cache.Remove(cacheKey);
            
            // Also remove from RPT cache
            rptCacheService.RemoveByUserId(userId);
            
            logger.LogDebug("Invalidated permissions cache for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error invalidating permissions cache for user {UserId}", userId);
        }

        return Task.CompletedTask;
    }

    private IReadOnlyList<Permission> ExtractPermissionsFromRpt(JwtSecurityToken rptToken)
    {
        var authorizationClaim = rptToken.Claims.FirstOrDefault(c => c.Type == "authorization");
        if (authorizationClaim == null)
        {
            return [];
        }

        return ExtractPermissionsFromAuthorizationClaim(authorizationClaim.Value);
    }

    /// <summary>
    /// Extracts permissions from a Keycloak RPT authorization claim JSON.
    /// Internal for testing purposes.
    /// </summary>
    /// <param name="authorizationClaimValue">The JSON value of the 'authorization' claim from an RPT token</param>
    /// <returns>List of Permission objects extracted from the claim</returns>
    internal static IReadOnlyList<Permission> ExtractPermissionsFromAuthorizationClaim(string authorizationClaimValue)
    {
        var permissions = new List<Permission>();

        try
        {
            using var document = JsonDocument.Parse(authorizationClaimValue);
            var root = document.RootElement;

            if (root.TryGetProperty("permissions", out var permissionsElement))
            {
                foreach (var permission in permissionsElement.EnumerateArray())
                {
                    if (permission.TryGetProperty("rsname", out var resourceName))
                    {
                        var resource = resourceName.GetString() ?? string.Empty;

                        // Extract scopes if present
                        if (permission.TryGetProperty("scopes", out var scopesArray))
                        {
                            var hasScopes = false;
                            foreach (var scope in scopesArray.EnumerateArray())
                            {
                                var action = scope.GetString();
                                if (!string.IsNullOrEmpty(action))
                                {
                                    permissions.Add(new Permission
                                    {
                                        Resource = resource,
                                        Action = action,
                                        Scope = action,
                                        Attributes = new Dictionary<string, object>()
                                    });
                                    hasScopes = true;
                                }
                            }

                            // Empty scopes array - add resource only
                            if (!hasScopes)
                            {
                                permissions.Add(new Permission
                                {
                                    Resource = resource,
                                    Action = string.Empty,
                                    Scope = string.Empty,
                                    Attributes = new Dictionary<string, object>()
                                });
                            }
                        }
                        else
                        {
                            // No scopes property - add resource only
                            permissions.Add(new Permission
                            {
                                Resource = resource,
                                Action = string.Empty,
                                Scope = string.Empty,
                                Attributes = new Dictionary<string, object>()
                            });
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Return empty list on parse failure
        }

        return permissions;
    }

    private async Task<string?> GetAccessTokenFromContext()
    {
        var context = httpContextAccessor.HttpContext;
        if (context?.User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        // Try to get access token from claims first
        var accessTokenClaim = context.User.FindFirst("access_token");
        if (accessTokenClaim != null)
        {
            return accessTokenClaim.Value;
        }

        // Fall back to authentication properties
        try
        {
            var accessToken = await context.GetTokenAsync("access_token");
            return accessToken;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve access token from authentication properties");
            return null;
        }
    }
}