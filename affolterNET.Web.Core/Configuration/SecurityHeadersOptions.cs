using affolterNET.Web.Core.Models;
using affolterNET.Web.Core.Options;

namespace affolterNET.Web.Core.Configuration;

/// <summary>
/// Security headers configuration options
/// </summary>
public class SecurityHeadersOptions: IConfigurableOptions<SecurityHeadersOptions>
{
    /// <summary>
    /// Configuration section name for binding from appsettings.json
    /// </summary>
    public static string SectionName => "affolterNET:Web:SecurityHeaders";

    public static SecurityHeadersOptions CreateDefaults(AppSettings settings)
    {
        return new SecurityHeadersOptions(settings);
    }

    public void CopyTo(SecurityHeadersOptions target)
    {
        target.Enabled = Enabled;
        target.IdpHost = IdpHost;
        target.AllowedConnectSources = new List<string>(AllowedConnectSources);
        target.AllowedScriptSources = new List<string>(AllowedScriptSources);
        target.AllowedStyleSources = new List<string>(AllowedStyleSources);
        target.AllowedImageSources = new List<string>(AllowedImageSources);
        target.AllowDataImages = AllowDataImages;
        target.AllowBlobImages = AllowBlobImages;
        target.AllowedFontSources = new List<string>(AllowedFontSources);
        target.RemoveServerHeader = RemoveServerHeader;
        target.HstsMaxAge = HstsMaxAge;
        target.HstsIncludeSubDomains = HstsIncludeSubDomains;
        target.HstsPreload = HstsPreload;
        target.CustomCspDirectives = new Dictionary<string, string>(CustomCspDirectives);
        target.XFrameOptions = XFrameOptions;
        target.XContentTypeOptions = XContentTypeOptions;
        target.ReferrerPolicy = ReferrerPolicy;
        target.CrossOriginOpenerPolicy = CrossOriginOpenerPolicy;
        target.CrossOriginResourcePolicy = CrossOriginResourcePolicy;
        target.CrossOriginEmbedderPolicy = CrossOriginEmbedderPolicy;
        target.PermissionsPolicy = PermissionsPolicy;
        target.EnableHsts = EnableHsts;
        target.FrontendUrl = FrontendUrl;
    }

    /// <summary>
    /// Parameterless constructor for options pattern compatibility
    /// </summary>
    public SecurityHeadersOptions() : this(new AppSettings())
    {
    }
    
    /// <summary>
    /// Constructor with settings parameter for meaningful defaults
    /// </summary>
    /// <param name="settings">Application settings containing development mode and authentication mode</param>
    private SecurityHeadersOptions(AppSettings settings)
    {
        Enabled = true;
        IdpHost = string.Empty;
        AllowedConnectSources = [];
        AllowedScriptSources = [];
        AllowedStyleSources = [];
        AllowedImageSources = [];
        AllowDataImages = true; // Allow data URLs for base64 images (commonly used)
        AllowBlobImages = true; // Allow blob images in development and production
        AllowedFontSources = []; // Allow jsdelivr CDN and data URIs for fonts (BeerCSS uses inline base64 fonts)
        RemoveServerHeader = true;
        HstsMaxAge = settings.IsDev ? 0 : 31536000; // Disable HSTS in development
        HstsIncludeSubDomains = !settings.IsDev; // More relaxed in development
        HstsPreload = !settings.IsDev; // Enable preload only in production
        CustomCspDirectives = new Dictionary<string, string>();
        
        // Security header defaults
        XFrameOptions = "DENY";
        XContentTypeOptions = "nosniff";
        ReferrerPolicy = "strict-origin-when-cross-origin";
        CrossOriginOpenerPolicy = "same-origin";
        CrossOriginResourcePolicy = settings.IsDev ? "cross-origin" : "same-origin"; // More relaxed for dev with proxy
        CrossOriginEmbedderPolicy = settings.IsDev ? string.Empty : "require-corp"; // Disabled in dev, enabled in prod
        PermissionsPolicy = "accelerometer=(), ambient-light-sensor=(), autoplay=(), battery=(), camera=(), " +
                           "cross-origin-isolated=(), display-capture=(), document-domain=(), encrypted-media=(), " +
                           "execution-while-not-rendered=(), execution-while-out-of-viewport=(), fullscreen=(), " +
                           "geolocation=(), gyroscope=(), keyboard-map=(), magnetometer=(), microphone=(), " +
                           "midi=(), navigation-override=(), payment=(), picture-in-picture=(), " +
                           "publickey-credentials-get=(), screen-wake-lock=(), sync-xhr=(), usb=(), " +
                           "web-share=(), xr-spatial-tracking=()";
        EnableHsts = !settings.IsDev; // Disable HSTS in development
        
        // Development-specific defaults
        if (settings.IsDev)
        {
            // Allow localhost for development
            AllowedConnectSources.Add("http://localhost:*");
            AllowedConnectSources.Add("https://localhost:*");
            AllowedConnectSources.Add("ws://localhost:*");
            AllowedConnectSources.Add("wss://localhost:*");
            
            // Add Vue/Vite dev server support
            AllowedScriptSources.Add("'unsafe-eval'"); // Required for Vue dev server hot reload
        }

        FrontendUrl = string.Empty;
    }

    /// <summary>
    /// Whether to enable security headers (default: true)
    /// </summary>
    public bool Enabled { get; set; }
    
    /// <summary>
    /// Identity provider host for Content Security Policy form-action directive
    /// </summary>
    public string IdpHost { get; set; }
    
    /// <summary>
    /// Additional allowed hosts for connect-src directive (API endpoints, WebSocket, etc.)
    /// </summary>
    public List<string> AllowedConnectSources { get; set; }
    
    /// <summary>
    /// Additional allowed hosts for script-src directive
    /// </summary>
    public List<string> AllowedScriptSources { get; set; }
    
    /// <summary>
    /// Additional allowed hosts for style-src directive
    /// </summary>
    public List<string> AllowedStyleSources { get; set; }
    
    /// <summary>
    /// Additional allowed hosts for img-src directive
    /// </summary>
    public List<string> AllowedImageSources { get; set; }
    
    /// <summary>
    /// Whether to allow data: URLs in img-src directive (default: true)
    /// </summary>
    public bool AllowDataImages { get; set; }
    
    /// <summary>
    /// Whether to allow blob: URLs in img-src directive (default: true for dev, false for prod)
    /// </summary>
    public bool AllowBlobImages { get; set; }
    
    /// <summary>
    /// Additional allowed hosts for font-src directive
    /// </summary>
    public List<string> AllowedFontSources { get; set; }
    
    /// <summary>
    /// Whether to remove the Server header (default: true)
    /// </summary>
    public bool RemoveServerHeader { get; set; }
    
    /// <summary>
    /// HSTS max age in seconds (default: 1 year)
    /// </summary>
    public int HstsMaxAge { get; set; }
    
    /// <summary>
    /// Whether to include subdomains in HSTS (default: true)
    /// </summary>
    public bool HstsIncludeSubDomains { get; set; }
    
    /// <summary>
    /// Whether to enable HSTS preload (default: false)
    /// </summary>
    public bool HstsPreload { get; set; }
    
    /// <summary>
    /// Custom CSP directives as key-value pairs
    /// </summary>
    public Dictionary<string, string> CustomCspDirectives { get; set; }
    
    // Security Header Configuration Properties
    
    /// <summary>
    /// X-Frame-Options header value (default: "DENY")
    /// </summary>
    public string XFrameOptions { get; set; }
    
    /// <summary>
    /// X-Content-Type-Options header value (default: "nosniff")
    /// </summary>
    public string XContentTypeOptions { get; set; }
    
    /// <summary>
    /// Referrer-Policy header value (default: "strict-origin-when-cross-origin")
    /// </summary>
    public string ReferrerPolicy { get; set; }
    
    /// <summary>
    /// Cross-Origin-Opener-Policy header value (default: "same-origin")
    /// </summary>
    public string CrossOriginOpenerPolicy { get; set; }
    
    /// <summary>
    /// Cross-Origin-Resource-Policy header value (default: "same-origin")
    /// </summary>
    public string CrossOriginResourcePolicy { get; set; }
    
    /// <summary>
    /// Cross-Origin-Embedder-Policy header value (default: empty, set to "require-corp" for production)
    /// </summary>
    public string CrossOriginEmbedderPolicy { get; set; }
    
    /// <summary>
    /// Permissions-Policy header value (default: restrictive policy disabling most features)
    /// </summary>
    public string PermissionsPolicy { get; set; }
    
    /// <summary>
    /// Whether to enable HSTS (Strict-Transport-Security) header
    /// </summary>
    public bool EnableHsts { get; set; }

    /// <summary>
    /// URL, where the frontend runs. This must be the dev server in development mode and the url of the bff in production.
    /// </summary>
    public string FrontendUrl { get; set; }
}