using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using affolterNET.Web.Bff.Services;

namespace affolterNET.Web.Bff.Controllers;

[ApiController]
[Route("bff/account")]
[IgnoreAntiforgeryToken]
public class BffController(IBffSessionService sessionService) : ControllerBase
{
    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? returnUrl = null, [FromQuery] string? claimsChallenge = null)
    {
        var redirectUri = !string.IsNullOrEmpty(returnUrl) ? returnUrl : "/";
        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUri
        };
        
        // Support claims challenge for conditional access scenarios
        if (!string.IsNullOrEmpty(claimsChallenge))
        {
            string jsonString = claimsChallenge.Replace("\\", "")
                .Trim(new char[1] { '"' });
            properties.Items["claims"] = jsonString;
        }
        
        return Challenge(properties);
    }

    [HttpGet("logout")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> LogoutGet([FromQuery] string? returnUrl = "/")
    {
        if (string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl))
            returnUrl = "/";

        if (HttpContext.User.Identity?.IsAuthenticated == true)
        {
            // Revoke tokens and sign out via OIDC (full logout)
            await sessionService.RevokeTokensAsync(HttpContext);
            return SignOut(
                new AuthenticationProperties { RedirectUri = returnUrl },
                CookieAuthenticationDefaults.AuthenticationScheme,
                OpenIdConnectDefaults.AuthenticationScheme);
        }

        // User is not authenticated (stale cookie, expired session, etc.)
        // Clear any remaining cookies and redirect
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect(returnUrl);
    }

    [HttpGet("logout-app-only")]
    [Authorize]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> LogoutAppOnly()
    {
        // Revoke tokens but don't logout from Keycloak
        await sessionService.RevokeTokensAsync(HttpContext);
        
        // Clear local authentication cookies only
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        
        // Redirect to home without going through Keycloak logout
        return Redirect("/");
    }
}