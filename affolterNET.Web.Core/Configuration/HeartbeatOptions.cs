using affolterNET.Web.Core.Models;
using affolterNET.Web.Core.Options;
using Microsoft.Extensions.Logging;

namespace affolterNET.Web.Core.Configuration;

/// <summary>
/// Heartbeat log line emitted by a background service for external liveness monitoring.
/// An Azure Log Search alert (or similar) greps logs for <see cref="Pattern"/> and fires
/// when no match is seen within a configured window — catching silently-stuck containers.
/// </summary>
public class HeartbeatOptions : IConfigurableOptions<HeartbeatOptions>
{
    public static string SectionName => "affolterNET:Web:Heartbeat";

    public static HeartbeatOptions CreateDefaults(AppSettings settings)
    {
        return new HeartbeatOptions(settings);
    }

    public HeartbeatOptions() : this(new AppSettings())
    {
    }

    private HeartbeatOptions(AppSettings settings)
    {
        Enabled = true;
        Pattern = "Heartbeat";
        IntervalSeconds = 300;
        LogLevel = LogLevel.Information;
    }

    public void CopyTo(HeartbeatOptions target)
    {
        target.Enabled = Enabled;
        target.Pattern = Pattern;
        target.IntervalSeconds = IntervalSeconds;
        target.LogLevel = LogLevel;
    }

    /// <summary>
    /// Emit heartbeat log lines. Default: true.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Token included in every heartbeat log line. The external monitor greps for this exact
    /// substring. Default: "Heartbeat". Override per-app for back-compat with existing alerts
    /// (e.g. GP-DataFlow's alert uses "SyncHeartbeat").
    /// </summary>
    public string Pattern { get; set; }

    /// <summary>
    /// Seconds between heartbeats. Default: 300 (5 minutes). Pick a value comfortably below
    /// your alert window so a single missed tick doesn't false-positive.
    /// </summary>
    public int IntervalSeconds { get; set; }

    /// <summary>
    /// Log level for the heartbeat line. Default: Information.
    /// </summary>
    public LogLevel LogLevel { get; set; }
}
