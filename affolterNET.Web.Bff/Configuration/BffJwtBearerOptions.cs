using affolterNET.Web.Core.Models;
using affolterNET.Web.Core.Options;

namespace affolterNET.Web.Bff.Configuration;

/// <summary>
/// JWT Bearer token validation configuration for BFF applications.
/// Enables a secondary auth scheme alongside the default Cookie/OIDC flow,
/// so API endpoints can accept Bearer tokens from non-browser clients.
/// </summary>
public class BffJwtBearerOptions : IConfigurableOptions<BffJwtBearerOptions>
{
    public static string SectionName => "affolterNET:Web:Bff:JwtBearer";

    public static BffJwtBearerOptions CreateDefaults(AppSettings appSettings)
    {
        return new BffJwtBearerOptions(appSettings);
    }

    public void CopyTo(BffJwtBearerOptions target)
    {
        target.Enabled = Enabled;
        target.ValidateIssuer = ValidateIssuer;
        target.ValidateAudience = ValidateAudience;
        target.ValidateLifetime = ValidateLifetime;
        target.ValidateIssuerSigningKey = ValidateIssuerSigningKey;
        target.RequireHttpsMetadata = RequireHttpsMetadata;
        target.ClockSkew = ClockSkew;
        target.ValidAudiences = ValidAudiences;
        target.ValidIssuers = ValidIssuers;
        target.ValidAuthorizedParties = ValidAuthorizedParties;
        target.RoleClaimType = RoleClaimType;
    }

    public BffJwtBearerOptions() : this(new AppSettings())
    {
    }

    private BffJwtBearerOptions(AppSettings appSettings)
    {
        Enabled = false;
        ValidateIssuer = true;
        ValidateAudience = true;
        ValidateLifetime = true;
        ValidateIssuerSigningKey = true;
        RequireHttpsMetadata = !appSettings.IsDev;
        ClockSkew = TimeSpan.FromMinutes(5);
        ValidAudiences = [];
        ValidIssuers = [];
        ValidAuthorizedParties = [];
        RoleClaimType = "roles";
    }

    /// <summary>
    /// Enable JWT Bearer as a secondary authentication scheme.
    /// Default: false (opt-in, no impact on existing BFF apps).
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Whether to validate the token issuer
    /// </summary>
    public bool ValidateIssuer { get; set; }

    /// <summary>
    /// Whether to validate the token audience
    /// </summary>
    public bool ValidateAudience { get; set; }

    /// <summary>
    /// Whether to validate token lifetime (expiration)
    /// </summary>
    public bool ValidateLifetime { get; set; }

    /// <summary>
    /// Whether to validate the issuer signing key
    /// </summary>
    public bool ValidateIssuerSigningKey { get; set; }

    /// <summary>
    /// Whether to require HTTPS metadata endpoints.
    /// Default: true in production, false in development.
    /// </summary>
    public bool RequireHttpsMetadata { get; set; }

    /// <summary>
    /// Allowed clock skew for token validation. Default: 5 minutes.
    /// </summary>
    public TimeSpan ClockSkew { get; set; }

    /// <summary>
    /// Valid audiences for token validation.
    /// Falls back to AuthProvider.ClientId if empty.
    /// </summary>
    public string[] ValidAudiences { get; set; }

    /// <summary>
    /// Valid issuers for token validation.
    /// Falls back to AuthProvider.Authority if empty.
    /// </summary>
    public string[] ValidIssuers { get; set; }

    /// <summary>
    /// Valid authorized parties (azp claim) for token validation.
    /// Common for Keycloak client credentials tokens where aud="account".
    /// When set, audience validation is automatically disabled and azp is validated instead.
    /// </summary>
    public string[] ValidAuthorizedParties { get; set; }

    /// <summary>
    /// Claim type used for role-based authorization.
    /// Default: "roles" (matches Keycloak/BFF OIDC setting).
    /// </summary>
    public string RoleClaimType { get; set; }
}
