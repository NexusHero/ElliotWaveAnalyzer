using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Builds and delivers the daily report: for every configured symbol it fetches the
/// technical analysis, renders a chart, and sends it through each enabled delivery channel.
///
/// Isolation by design: a failure for one symbol or one channel is logged and swallowed so
/// the rest of the report still goes out (a flaky Telegram call must not block the email,
/// and a missing symbol must not block the others).
/// </summary>
public sealed class DailyReportService(
    ITechnicalAnalysisService analysisService,
    IChartRenderer chartRenderer,
    IEnumerable<IReportDeliveryChannel> channels,
    IOptions<DailyReportOptions> options,
    ILogger<DailyReportService> logger) : IDailyReportService
{
    private readonly IReadOnlyList<IReportDeliveryChannel> _channels = [.. channels];

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        var enabledChannels = _channels.Where(c => c.IsEnabled).ToList();

        if (enabledChannels.Count == 0)
        {
            logger.LogWarning("Daily report skipped: no delivery channels are enabled.");
            return;
        }

        logger.LogInformation(
            "Running daily report for {Symbols} via {Channels}",
            string.Join(", ", opts.Symbols),
            string.Join(", ", enabledChannels.Select(c => c.Name)));

        foreach (var symbol in opts.Symbols)
        {
            await DeliverSymbolAsync(symbol, opts.Days, enabledChannels, cancellationToken);
        }
    }

    private async Task DeliverSymbolAsync(
        string symbol,
        int days,
        IReadOnlyList<IReportDeliveryChannel> channels,
        CancellationToken cancellationToken)
    {
        ReportArtifact artifact;
        try
        {
            var analysis = await analysisService.GetAnalysisAsync(symbol, days, cancellationToken: cancellationToken);
            var png = chartRenderer.RenderPng(analysis);
            artifact = new ReportArtifact(
                symbol,
                png,
                $"Daily {symbol} report — {analysis.Candles.Count} candles, RSI & MACD included.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to build daily report for {Symbol}; skipping it", symbol);
            return;
        }

        foreach (var channel in channels)
        {
            try
            {
                await channel.SendAsync(artifact, cancellationToken);
                logger.LogInformation("Delivered {Symbol} report via {Channel}", symbol, channel.Name);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to deliver {Symbol} report via {Channel}", symbol, channel.Name);
            }
        }
    }
}
