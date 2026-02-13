using affolterNET.Web.Bff.Configuration;
using affolterNET.Web.Core.Configuration;
using affolterNET.Web.Core.Extensions;
using affolterNET.Web.Core.Options;
using affolterNET.Web.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace affolterNET.Web.Bff.Options;

/// <summary>
/// Configuration options for BFF application pipeline
/// </summary>
public class BffAppOptions : CoreAppOptions
{
    public BffAppOptions(AppSettings appSettings, IConfiguration config) : base(appSettings, config)
    {
        AntiForgery = config.CreateFromConfig<BffAntiforgeryOptions>(appSettings);
        Bff = config.CreateFromConfig<BffOptions>(appSettings);
        if (string.IsNullOrWhiteSpace(Bff.FrontendUrl))
        {
            // common scenario for bff applications - only dev setups have differences here
            Bff.FrontendUrl = Bff.BackendUrl;
        }

        CookieAuth = config.CreateFromConfig<CookieAuthOptions>(appSettings);
        Rpt = config.CreateFromConfig<RptOptions>(appSettings);
        BffAuth = config.CreateFromConfig<BffAuthOptions>(appSettings);
        DataProtection = config.CreateFromConfig<DataProtectionOptions>(appSettings);
    }

    public BffAuthOptions BffAuth { get; set; }
    public Action<BffAuthOptions>? ConfigureAuth { get; set; }

    public BffAntiforgeryOptions AntiForgery { get; set; }
    public Action<BffAntiforgeryOptions>? ConfigureAntiForgery { get; set; }

    public BffOptions Bff { get; set; }
    public Action<BffOptions>? ConfigureBff { get; set; }

    public CookieAuthOptions CookieAuth { get; set; }
    public Action<CookieAuthOptions>? ConfigureCookieAuth { get; set; }

    public RptOptions Rpt { get; set; }
    public Action<RptOptions>? ConfigureRpt { get; set; }

    public DataProtectionOptions DataProtection { get; set; }
    public Action<DataProtectionOptions>? ConfigureDataProtection { get; set; }
    
    /// <summary>
    /// Configuration action for custom middleware - called before endpoint mapping
    /// </summary>
    public Action<IApplicationBuilder>? ConfigureBeforeEndpointsCustomMiddleware { get; set; }
    
    /// <summary>
    /// Configuration action for custom middleware - called after routing
    /// </summary>
    public Action<IApplicationBuilder>? ConfigureAfterRoutingCustomMiddleware { get; set; }

    public void Configure(IServiceCollection services)
    {
        var actions = new ConfigureActions();
        actions.Add(ConfigureAntiForgery);
        actions.Add(ConfigureAuth);
        actions.Add(ConfigureBff);
        actions.Add(ConfigureCookieAuth);
        actions.Add(ConfigureRpt);
        actions.Add(ConfigureDataProtection);

        AntiForgery.RunActions(actions);
        BffAuth.RunActions(actions);
        Bff.RunActions(actions);
        CookieAuth.RunActions(actions);
        Rpt.RunActions(actions);
        DataProtection.RunActions(actions);
        RunCoreActions(actions);

        // Move config values to base types
        var baseActions = new ConfigureActions();
        Action<SecurityHeadersOptions> configureUrl = sho =>
        {
            sho.FrontendUrl = Bff.FrontendUrl;
            sho.IdpHost = AuthProvider.AuthorityBase;
        };
        baseActions.Add(configureUrl);
        RunCoreActions(baseActions);

        // configure DI
        AntiForgery.ConfigureDi(services);
        BffAuth.ConfigureDi(services);
        Bff.ConfigureDi(services);
        CookieAuth.ConfigureDi(services);
        Rpt.ConfigureDi(services);
        DataProtection.ConfigureDi(services);

        // Core configuration
        ConfigureCoreDi(services);
    }

    protected override Dictionary<string, object> GetConfigs()
    {
        var configDict = new Dictionary<string, object>();
        BffAuth.AddToConfigurationDict(configDict);
        AntiForgery.AddToConfigurationDict(configDict);
        Bff.AddToConfigurationDict(configDict);
        CookieAuth.AddToConfigurationDict(configDict);
        Rpt.AddToConfigurationDict(configDict);
        DataProtection.AddToConfigurationDict(configDict);
        return configDict;
    }

    public void ValidateConfiguration()
    {
        var errors = new List<string>
        {
            AuthProvider.CheckNullOrWhitespace(x => x.AuthorityBase),
            AuthProvider.CheckNullOrWhitespace(x => x.Realm),
            AuthProvider.CheckNullOrWhitespace(x => x.ClientId),
            AuthProvider.CheckNullOrWhitespace(x => x.ClientSecret),
            BffAuth.CheckNullOrWhitespace(x => x.CallbackPath),
            BffAuth.CheckNullOrWhitespace(x => x.SignoutCallback),
            BffAuth.CheckNullOrWhitespace(x => x.PostLogoutRedirectUri),
            BffAuth.CheckNullOrWhitespace(x => x.RedirectUri),
            Bff.CheckNullOrWhitespace(x => x.BackendUrl)
        };

        var realErrors = errors.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (realErrors.Any())
        {
            throw new ApplicationException("Invalid BffAppOptions configuration: \n" + string.Join("\n", realErrors));
        }
    }
}