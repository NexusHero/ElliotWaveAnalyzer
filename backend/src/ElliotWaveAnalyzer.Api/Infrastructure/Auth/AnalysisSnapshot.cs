using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// A persisted analysis in a user's track record. Stores only the deterministic geometry that
/// defines the outcome (direction, invalidation line, target zone) plus the metadata shown in
/// the history; the outcome itself is computed on read against live candles, never stored, so
/// it always reflects the latest price action.
/// </summary>
internal sealed class AnalysisSnapshot
{
    public Guid Id { get; set; }

    /// <summary>Owner. Every query is scoped by this — no cross-user access.</summary>
    public Guid UserId { get; set; }

    public string Symbol { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public string Structure { get; set; } = string.Empty;
    public bool Bullish { get; set; }

    public decimal? InvalidationPrice { get; set; }

    /// <summary>True when the invalidation line sits above price (a move up voids the count).</summary>
    public bool InvalidationAbove { get; set; }

    public decimal? TargetLow { get; set; }
    public decimal? TargetHigh { get; set; }

    /// <summary>Entry (pullback) zone of the current primary — the band that fires a zone-entry alert.</summary>
    public decimal? EntryLow { get; set; }
    public decimal? EntryHigh { get; set; }

    public string Confidence { get; set; } = string.Empty;
    public decimal? Score { get; set; }

    /// <summary>
    /// The persona-panel (#184) persona whose own top pick this saved analysis was, or null for
    /// every analysis saved outside the panel (the vast majority). Only tagged saves feed
    /// <see cref="Interfaces.IPersonaCalibrationProvider"/> — untagged analyses carry no signal
    /// about any individual persona's reliability.
    /// </summary>
    public string? Persona { get; set; }

    /// <summary>
    /// The outcome this analysis was last alerted on. Starts at <see cref="AnalysisOutcome.Pending"/>;
    /// advanced to a terminal outcome once an alert has fired, so a transition alerts exactly once.
    /// The auto-switch resets it to Pending when it promotes an alternate, so the new primary is
    /// evaluated afresh.
    /// </summary>
    public AnalysisOutcome AlertedOutcome { get; set; } = AnalysisOutcome.Pending;

    /// <summary>True once a "price entered the entry zone" alert has fired — idempotency, no re-fire.</summary>
    public bool EntryZoneAlerted { get; set; }

    /// <summary>The scenario tree: the primary in force plus its alternates and any retired former primaries.</summary>
    public List<AnalysisScenarioRow> Scenarios { get; set; } = [];

    /// <summary>Append-only auto-switch audit trail.</summary>
    public List<AnalysisSwitchEventRow> SwitchEvents { get; set; } = [];
}
