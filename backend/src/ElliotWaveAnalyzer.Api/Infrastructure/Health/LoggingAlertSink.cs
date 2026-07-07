using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Health;

/// <summary>
/// Default <see cref="IAlertSink"/> (#173 AC3): a structured Critical-level log line, always
/// registered so an alert genuinely fires even with no paging vendor configured — an operator's
/// log aggregator (whatever it is) can alert on this level/message without this app knowing
/// anything about it. Wiring a real paging/chat integration is a named follow-on (see the
/// operations runbook), not a silently-missing feature.
/// </summary>
internal sealed class LoggingAlertSink(ILogger<LoggingAlertSink> logger) : IAlertSink
{
    public Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        logger.LogCritical("OPERATOR ALERT: {Message}", message);
        return Task.CompletedTask;
    }
}
