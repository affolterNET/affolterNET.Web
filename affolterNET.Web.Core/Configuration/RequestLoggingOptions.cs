using affolterNET.Web.Core.Models;
using affolterNET.Web.Core.Options;

namespace affolterNET.Web.Core.Configuration;

public class RequestLoggingOptions : IConfigurableOptions<RequestLoggingOptions>
{
    public static string SectionName => "affolterNET:Web:RequestLogging";

    public static RequestLoggingOptions CreateDefaults(AppSettings settings)
    {
        return new RequestLoggingOptions(settings);
    }

    public RequestLoggingOptions() : this(new AppSettings())
    {
    }

    public void CopyTo(RequestLoggingOptions target)
    {
        target.Enabled = Enabled;
        target.ExcludePaths = ExcludePaths;
    }

    private RequestLoggingOptions(AppSettings settings)
    {
        Enabled = false;
        ExcludePaths = ["/health/"];
    }

    /// <summary>
    /// Enable request logging middleware (default: false)
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Path prefixes to exclude from logging (default: ["/health/"])
    /// </summary>
    public string[] ExcludePaths { get; set; }
}
