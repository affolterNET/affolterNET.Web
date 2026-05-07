namespace affolterNET.Web.Core.Models;

/// <summary>
/// Application settings for affolterNET.Web configuration
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Whether the application is running in development mode
    /// </summary>
    public bool IsDev { get; set; }

    /// <summary>
    /// Authentication mode for the BFF application
    /// </summary>
    public AuthenticationMode AuthMode { get; set; }

    public AppSettings() : this(false, AuthenticationMode.None)
    {
    }

    /// <summary>
    /// Constructor with parameters
    /// </summary>
    /// <param name="isDev">Whether running in development mode</param>
    /// <param name="authMode">Authentication mode</param>
    public AppSettings(bool isDev, AuthenticationMode authMode)
    {
        IsDev = isDev;
        AuthMode = authMode;
    }
}
