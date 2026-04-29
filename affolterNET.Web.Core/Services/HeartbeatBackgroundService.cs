using affolterNET.Web.Core.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace affolterNET.Web.Core.Services;

/// <summary>
/// Emits a heartbeat log line at a fixed interval so external monitors can detect
/// silently-stuck containers. The line contains the configured <see cref="HeartbeatOptions.Pattern"/>
/// substring, which an Azure Log Search alert (or equivalent) greps for.
/// </summary>
public class HeartbeatBackgroundService(
    IOptions<HeartbeatOptions> options,
    ILogger<HeartbeatBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        if (!opts.Enabled)
        {
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(1, opts.IntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            // Format chosen to match the historical "SyncHeartbeat: alive at <ISO-8601 UTC>"
            // line that existing log-search alerts already grep for.
            logger.Log(opts.LogLevel, "{Pattern}: alive at {Timestamp:O}",
                opts.Pattern, DateTimeOffset.UtcNow);

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
