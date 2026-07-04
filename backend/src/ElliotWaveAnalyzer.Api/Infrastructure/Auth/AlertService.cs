using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// Re-evaluates still-pending saved analyses and delivers an alert for each transition to a
/// terminal outcome (invalidated / target reached). Orchestrator only: the outcome maths is the
/// pure <see cref="AnalysisOutcomeEvaluator"/>, the alert decision is the pure
/// <see cref="AlertDecision"/>, and delivery goes through the existing
/// <see cref="IReportDeliveryChannel"/> abstractions — so this class just wires them and persists
/// the advanced <see cref="AnalysisSnapshot.AlertedOutcome"/>.
///
/// Isolation by design (mirrors <c>DailyReportService</c>): a failure for one symbol or one
/// channel is logged and swallowed so the rest of the pass still completes.
/// </summary>
internal sealed class AlertService(
    AppDbContext db,
    ITechnicalAnalysisService analysisService,
    IChartRenderer chartRenderer,
    IEnumerable<IReportDeliveryChannel> channels,
    ILogger<AlertService> logger) : IAlertService
{
    private readonly IReadOnlyList<IReportDeliveryChannel> _channels = [.. channels];

    /// <inheritdoc/>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var enabledChannels = _channels.Where(c => c.IsEnabled).ToList();
        if (enabledChannels.Count == 0)
        {
            logger.LogWarning("Alert pass skipped: no delivery channels are enabled.");
            return 0;
        }

        // Only still-pending analyses can transition; terminal ones are settled and never re-fire.
        var pending = await db.AnalysisSnapshots
            .Where(s => s.AlertedOutcome == AnalysisOutcome.Pending)
            .ToListAsync(cancellationToken);
        if (pending.Count == 0)
        {
            return 0;
        }

        var alertsSent = 0;
        // One market fetch + render per symbol; every snapshot of that symbol reuses it.
        foreach (var bySymbol in pending.GroupBy(s => s.Symbol, StringComparer.OrdinalIgnoreCase))
        {
            var context = await LoadSymbolAsync(bySymbol.Key, cancellationToken);
            if (context is null)
            {
                continue; // fetch/render failure already logged; snapshots stay pending
            }

            foreach (var snapshot in bySymbol)
            {
                if (TryBuildAlert(snapshot, context.Value.Candles, out var newOutcome))
                {
                    var artifact = BuildArtifact(snapshot, newOutcome, context.Value.Png);
                    await DeliverAsync(artifact, enabledChannels, cancellationToken);
                    snapshot.AlertedOutcome = newOutcome;
                    alertsSent++;
                }
            }
        }

        if (alertsSent > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Delivered {Count} price alert(s)", alertsSent);
        }

        return alertsSent;
    }

    /// <summary>Fetches candles + renders the chart once for a symbol; null on any failure.</summary>
    private async Task<(IReadOnlyList<MarketCandle> Candles, byte[] Png)?> LoadSymbolAsync(
        string symbol, CancellationToken cancellationToken)
    {
        try
        {
            var analysis = await analysisService.GetAnalysisAsync(symbol, cancellationToken: cancellationToken);
            var png = chartRenderer.RenderPng(analysis);
            return (analysis.Candles, png);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not load market data for {Symbol}; its alerts wait for the next pass", symbol);
            return null;
        }
    }

    /// <summary>Evaluates the snapshot against the candles and applies the pure alert decision.</summary>
    private static bool TryBuildAlert(
        AnalysisSnapshot snapshot, IReadOnlyList<MarketCandle> candles, out AnalysisOutcome newOutcome)
    {
        var after = candles.Where(c => c.OpenTime > snapshot.CreatedAt).ToList();
        var evaluation = AnalysisOutcomeEvaluator.Evaluate(
            snapshot.Bullish,
            snapshot.InvalidationPrice,
            snapshot.InvalidationAbove,
            snapshot.TargetLow,
            snapshot.TargetHigh,
            after);

        var alert = AlertDecision.NewAlert(snapshot.AlertedOutcome, evaluation.Outcome);
        newOutcome = alert ?? AnalysisOutcome.Pending;
        return alert is not null;
    }

    private static ReportArtifact BuildArtifact(AnalysisSnapshot s, AnalysisOutcome outcome, byte[] png)
    {
        var headline = outcome == AnalysisOutcome.TargetReached ? "🎯 target reached" : "⚠️ invalidated";
        var direction = s.Bullish ? "bullish" : "bearish";
        var caption =
            $"{headline} — {s.Symbol} {s.Structure} ({direction}). "
            + $"Saved {s.CreatedAt:yyyy-MM-dd}. Not financial advice.";
        return new ReportArtifact(s.Symbol, png, caption);
    }

    private async Task DeliverAsync(
        ReportArtifact artifact, IReadOnlyList<IReportDeliveryChannel> channels, CancellationToken cancellationToken)
    {
        foreach (var channel in channels)
        {
            try
            {
                await channel.SendAsync(artifact, cancellationToken);
                logger.LogInformation("Delivered {Symbol} alert via {Channel}", artifact.Symbol, channel.Name);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to deliver {Symbol} alert via {Channel}", artifact.Symbol, channel.Name);
            }
        }
    }
}
