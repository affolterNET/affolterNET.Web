using affolterNET.Web.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace affolterNET.Web.Core.Services;

public class HeartbeatBackgroundServiceTests
{
    [Fact]
    public async Task EmitsLogLineContainingPattern_WhenEnabled()
    {
        var opts = new HeartbeatOptions
        {
            Enabled = true,
            Pattern = "TestHeartbeat",
            IntervalSeconds = 1,
            LogLevel = LogLevel.Information
        };
        var logger = new RecordingLogger<HeartbeatBackgroundService>();
        var svc = new HeartbeatBackgroundService(Microsoft.Extensions.Options.Options.Create(opts), logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        await svc.StartAsync(cts.Token);
        try { await Task.Delay(100, cts.Token); } catch (OperationCanceledException) { }
        await svc.StopAsync(CancellationToken.None);

        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.Information && e.Message.Contains("TestHeartbeat"));
    }

    [Fact]
    public async Task DoesNotLog_WhenDisabled()
    {
        var opts = new HeartbeatOptions { Enabled = false, Pattern = "X", IntervalSeconds = 1 };
        var logger = new RecordingLogger<HeartbeatBackgroundService>();
        var svc = new HeartbeatBackgroundService(Microsoft.Extensions.Options.Options.Create(opts), logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await svc.StartAsync(cts.Token);
        try { await Task.Delay(40, cts.Token); } catch (OperationCanceledException) { }
        await svc.StopAsync(CancellationToken.None);

        Assert.Empty(logger.Entries);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
