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

    public string Confidence { get; set; } = string.Empty;
    public decimal? Score { get; set; }

    /// <summary>
    /// The outcome this analysis was last alerted on. Starts at <see cref="AnalysisOutcome.Pending"/>;
    /// advanced to a terminal outcome once an alert has fired, so a transition alerts exactly once.
    /// </summary>
    public AnalysisOutcome AlertedOutcome { get; set; } = AnalysisOutcome.Pending;
}
