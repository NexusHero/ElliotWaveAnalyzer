using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// Re-evaluates still-pending saved analyses each pass and, for each, delivers up to two kinds of
/// alert: a <b>zone-entry</b> alert when price first trades into the primary's entry zone, and an
/// <b>outcome</b> alert on the transition to a terminal outcome (invalidated / target reached).
/// When the primary is invalidated the pass also runs the <b>auto-switch</b>: the best surviving
/// alternate is promoted to primary, the old primary is retired for the audit trail, a switch event
/// is appended, and the analysis re-opens as pending so the new primary is tracked afresh.
///
/// Orchestrator only: the outcome maths is the pure <see cref="AnalysisOutcomeEvaluator"/>, the
/// alert/zone/switch decisions are the pure <see cref="AlertDecision"/> / <see cref="ZoneEntryDecision"/>
/// / <see cref="ScenarioSwitch"/>, and delivery goes through the existing
/// <see cref="IReportDeliveryChannel"/> abstractions.
///
/// Isolation by design (mirrors <c>DailyReportService</c>): a failure for one symbol or one
/// channel is logged and swallowed so the rest of the pass still completes.
/// </summary>
internal sealed class AlertService(
    AppDbContext db,
    ITechnicalAnalysisService analysisService,
    IChartRenderer chartRenderer,
    IEnumerable<IReportDeliveryChannel> channels,
    TimeProvider timeProvider,
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
            .Include(s => s.Scenarios)
            .Include(s => s.SwitchEvents)
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
                alertsSent += await ProcessSnapshotAsync(
                    snapshot, context.Value.Candles, context.Value.Png, enabledChannels, cancellationToken);
            }
        }

        if (alertsSent > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Delivered {Count} price alert(s)", alertsSent);
        }

        return alertsSent;
    }

    /// <summary>
    /// Evaluates one snapshot: fires the zone-entry alert (once), then the outcome alert; on
    /// invalidation runs the auto-switch. Returns how many alerts were delivered for it.
    /// </summary>
    private async Task<int> ProcessSnapshotAsync(
        AnalysisSnapshot snapshot,
        IReadOnlyList<MarketCandle> candles,
        byte[] png,
        IReadOnlyList<IReportDeliveryChannel> channels,
        CancellationToken cancellationToken)
    {
        var after = candles.Where(c => c.OpenTime > snapshot.CreatedAt).ToList();
        var delivered = 0;

        // Zone entry — evaluated against the current primary's entry band, at most once.
        if (ZoneEntryDecision.ShouldAlert(snapshot.EntryLow, snapshot.EntryHigh, snapshot.EntryZoneAlerted, after))
        {
            await DeliverAsync(BuildArtifact(snapshot, EntryZoneCaption(snapshot), png), channels, cancellationToken);
            snapshot.EntryZoneAlerted = true;
            delivered++;
        }

        // Outcome — invalidation / target, exactly once per pending→terminal transition.
        var evaluation = AnalysisOutcomeEvaluator.Evaluate(
            snapshot.Bullish, snapshot.InvalidationPrice, snapshot.InvalidationAbove,
            snapshot.TargetLow, snapshot.TargetHigh, after);
        var alert = AlertDecision.NewAlert(snapshot.AlertedOutcome, evaluation.Outcome);
        if (alert is null)
        {
            return delivered;
        }

        await DeliverAsync(BuildArtifact(snapshot, OutcomeCaption(snapshot, alert.Value), png), channels, cancellationToken);
        delivered++;

        if (alert.Value == AnalysisOutcome.Invalidated && TryAutoSwitch(snapshot))
        {
            // Promoted an alternate — the snapshot re-opens as pending under the new primary.
            return delivered;
        }

        snapshot.AlertedOutcome = alert.Value;
        return delivered;
    }

    /// <summary>
    /// Promotes the best surviving alternate to primary, retires the old primary, appends a switch
    /// event, and re-opens the snapshot as pending. Returns false when there is no alternate.
    /// </summary>
    private bool TryAutoSwitch(AnalysisSnapshot snapshot)
    {
        var alternates = snapshot.Scenarios
            .Where(r => r is { Role: ScenarioRole.Alternate, Retired: false })
            .Select(ToScenario)
            .ToList();

        var promotion = ScenarioSwitch.SelectPromotion(alternates);
        if (promotion is null)
        {
            return false;
        }

        var oldPrimary = snapshot.Scenarios.FirstOrDefault(r => r is { Role: ScenarioRole.Primary, Retired: false });
        var promoted = snapshot.Scenarios.First(r => r.Label == promotion.Label && !r.Retired);

        if (oldPrimary is not null)
        {
            oldPrimary.Retired = true; // kept in the tree for the switch history
        }

        promoted.Role = ScenarioRole.Primary;

        // The flat snapshot fields track the active primary — sync them so the evaluator reads the
        // promoted scenario from now on.
        snapshot.Structure = promoted.Structure;
        snapshot.Bullish = promoted.Bullish;
        snapshot.InvalidationPrice = promoted.InvalidationPrice;
        snapshot.InvalidationAbove = promoted.InvalidationAbove;
        snapshot.TargetLow = promoted.TargetLow;
        snapshot.TargetHigh = promoted.TargetHigh;
        snapshot.EntryLow = promoted.EntryLow;
        snapshot.EntryHigh = promoted.EntryHigh;
        snapshot.Confidence = promoted.Confidence;
        snapshot.Score = promoted.Score;

        snapshot.AlertedOutcome = AnalysisOutcome.Pending;
        snapshot.EntryZoneAlerted = false;

        // Add through the context, not the parent's navigation: the snapshot is already tracked, so
        // a new child with a client-set key added via the collection is misread as Modified (→ an
        // UPDATE that matches no row → DbUpdateConcurrencyException). db.Add forces the Added state.
        db.Add(new AnalysisSwitchEventRow
        {
            Id = Guid.NewGuid(),
            AnalysisSnapshotId = snapshot.Id,
            At = timeProvider.GetUtcNow(),
            FromLabel = oldPrimary?.Label ?? "Primary",
            ToLabel = promoted.Label,
            Reason = "primary invalidation breached",
        });

        logger.LogInformation(
            "Auto-switched analysis {Id}: {From} → {To}", snapshot.Id, oldPrimary?.Label, promoted.Label);
        return true;
    }

    private static Scenario ToScenario(AnalysisScenarioRow r) => new(
        r.Role, r.Label, r.Structure, r.Bullish, r.InvalidationPrice, r.InvalidationAbove,
        r.EntryLow, r.EntryHigh, r.TargetLow, r.TargetHigh, r.Confidence, r.Score,
        Probability: null, ProbabilityBasis.InsufficientData, r.Retired);

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

    private static string OutcomeCaption(AnalysisSnapshot s, AnalysisOutcome outcome)
    {
        var headline = outcome == AnalysisOutcome.TargetReached ? "🎯 target reached" : "⚠️ invalidated";
        return $"{headline} — {s.Symbol} {s.Structure} ({Direction(s)}). "
            + $"Saved {s.CreatedAt:yyyy-MM-dd}. Not financial advice.";
    }

    private static string EntryZoneCaption(AnalysisSnapshot s)
        => $"📍 entry zone reached — {s.Symbol} {s.Structure} ({Direction(s)}). "
            + $"Price entered the long-term entry zone. Not financial advice.";

    private static string Direction(AnalysisSnapshot s) => s.Bullish ? "bullish" : "bearish";

    private static ReportArtifact BuildArtifact(AnalysisSnapshot s, string caption, byte[] png)
        => new(s.Symbol, png, caption);

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
