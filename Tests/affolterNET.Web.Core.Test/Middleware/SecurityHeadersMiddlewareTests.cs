using affolterNET.Web.Core.Configuration;
using affolterNET.Web.Core.Middleware;

namespace affolterNET.Web.Core.Test.Middleware;

public class SecurityHeadersMiddlewareTests
{
    private static SecurityHeadersOptions CreateDefaultOptions() => new();

    [Fact]
    public void DefaultCsp_ContainsNonceAndStrictDynamic()
    {
        var options = CreateDefaultOptions();
        var csp = SecurityHeadersMiddleware.BuildContentSecurityPolicy(options, "test-nonce");

        Assert.Contains("script-src 'nonce-test-nonce' 'strict-dynamic'", csp);
    }

    [Fact]
    public void DefaultCsp_ContainsAllExpectedDirectives()
    {
        var options = CreateDefaultOptions();
        var csp = SecurityHeadersMiddleware.BuildContentSecurityPolicy(options, "n");

        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("object-src 'none'", csp);
        Assert.Contains("base-uri 'self'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("img-src", csp);
        Assert.Contains("font-src 'self'", csp);
        Assert.Contains("form-action 'self'", csp);
        Assert.Contains("script-src", csp);
        Assert.Contains("style-src", csp);
        Assert.Contains("connect-src 'self'", csp);
    }

    [Fact]
    public void CustomScriptSrc_OverridesDefault()
    {
        var options = CreateDefaultOptions();
        options.CustomCspDirectives["script-src"] = "'self'";

        var csp = SecurityHeadersMiddleware.BuildContentSecurityPolicy(options, "test-nonce");

        Assert.Contains("script-src 'self'", csp);
        Assert.DoesNotContain("nonce-", csp);
        Assert.DoesNotContain("strict-dynamic", csp);
    }

    [Fact]
    public void CustomConnectSrc_OverridesDefault()
    {
        var options = CreateDefaultOptions();
        options.AllowedConnectSources.Add("https://api.example.com");
        options.CustomCspDirectives["connect-src"] = "'self' https://custom.example.com";

        var csp = SecurityHeadersMiddleware.BuildContentSecurityPolicy(options, "n");

        Assert.Contains("connect-src 'self' https://custom.example.com", csp);
        // The built-in AllowedConnectSources should NOT appear in the connect-src directive
        Assert.DoesNotContain("https://api.example.com", csp);
    }

    [Fact]
    public void CustomImgSrc_OverridesDefault()
    {
        var options = CreateDefaultOptions();
        options.AllowDataImages = true;
        options.AllowBlobImages = true;
        options.CustomCspDirectives["img-src"] = "'self'";

        var csp = SecurityHeadersMiddleware.BuildContentSecurityPolicy(options, "n");

        Assert.Contains("img-src 'self'", csp);
        Assert.DoesNotContain("data:", csp);
        Assert.DoesNotContain("blob:", csp);
    }

    [Fact]
    public void CustomFormAction_OverridesDefault()
    {
        var options = CreateDefaultOptions();
        options.IdpHost = "https://idp.example.com";
        options.CustomCspDirectives["form-action"] = "'self'";

        var csp = SecurityHeadersMiddleware.BuildContentSecurityPolicy(options, "n");

        // form-action should use custom value, not include IdpHost
        Assert.Contains("form-action 'self'", csp);
        Assert.DoesNotContain("form-action 'self' https://idp.example.com", csp);
    }

    [Fact]
    public void CustomDefaultSrc_OverridesDefault()
    {
        var options = CreateDefaultOptions();
        options.CustomCspDirectives["default-src"] = "'self' https://cdn.example.com";

        var csp = SecurityHeadersMiddleware.BuildContentSecurityPolicy(options, "n");

        Assert.Contains("default-src 'self' https://cdn.example.com", csp);
        // Should only appear once (the custom one)
        Assert.Equal(1, csp.Split("default-src").Length - 1);
    }

    [Fact]
    public void CustomObjectSrc_OverridesDefault()
    {
        var options = CreateDefaultOptions();
        options.CustomCspDirectives["object-src"] = "'self'";

        var csp = SecurityHeadersMiddleware.BuildContentSecurityPolicy(options, "n");

        Assert.Contains("object-src 'self'", csp);
        Assert.DoesNotContain("object-src 'none'", csp);
    }

    [Fact]
    public void CustomFrameAncestors_OverridesDefault()
    {
        var options = CreateDefaultOptions();
        options.CustomCspDirectives["frame-ancestors"] = "'self'";

        var csp = SecurityHeadersMiddleware.BuildContentSecurityPolicy(options, "n");

        Assert.Contains("frame-ancestors 'self'", csp);
        Assert.DoesNotContain("frame-ancestors 'none'", csp);
    }

    [Fact]
    public void CustomStyleSrc_OverridesDefault()
    {
        var options = CreateDefaultOptions();
        options.CustomCspDirectives["style-src"] = "'self' 'unsafe-inline'";

        var csp = SecurityHeadersMiddleware.BuildContentSecurityPolicy(options, "n");

        Assert.Contains("style-src 'self' 'unsafe-inline'", csp);
        // Should only appear once
        Assert.Equal(1, csp.Split("style-src").Length - 1);
    }

    [Fact]
    public void MultipleCustomDirectives_AllApplied()
    {
        var options = CreateDefaultOptions();
        options.CustomCspDirectives["script-src"] = "'self'";
        options.CustomCspDirectives["connect-src"] = "'self' https://api.example.com";
        options.CustomCspDirectives["worker-src"] = "'self' blob:";

        var csp = SecurityHeadersMiddleware.BuildContentSecurityPolicy(options, "n");

        Assert.Contains("script-src 'self'", csp);
        Assert.Contains("connect-src 'self' https://api.example.com", csp);
        Assert.Contains("worker-src 'self' blob:", csp);
        Assert.DoesNotContain("nonce-", csp);
        Assert.DoesNotContain("strict-dynamic", csp);
    }

    [Fact]
    public void NoCustomDirectives_DefaultBehaviorPreserved()
    {
        var options = CreateDefaultOptions();
        options.IdpHost = "https://idp.example.com";
        options.FrontendUrl = "https://app.example.com";
        options.AllowedConnectSources.Add("https://api.example.com");

        var csp = SecurityHeadersMiddleware.BuildContentSecurityPolicy(options, "abc123");

        Assert.Contains("script-src 'nonce-abc123' 'strict-dynamic'", csp);
        Assert.Contains("connect-src 'self' https://api.example.com https://idp.example.com https://app.example.com wss://app.example.com", csp);
        Assert.Contains("form-action 'self' https://idp.example.com", csp);
    }
}
