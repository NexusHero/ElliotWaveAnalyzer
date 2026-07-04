using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Pure decision for whether a re-evaluated analysis warrants a new alert. An alert fires
/// exactly once, on the transition from the last-alerted outcome to a terminal one: a saved
/// analysis that was still <see cref="AnalysisOutcome.Pending"/> and has now become
/// <see cref="AnalysisOutcome.Invalidated"/> or <see cref="AnalysisOutcome.TargetReached"/>.
///
/// Kept pure/static (no I/O) — same pattern as the other Application checkers — so the "should
/// we notify?" logic is exhaustively unit-testable without a database, scheduler, or channel.
/// </summary>
public static class AlertDecision
{
    /// <summary>
    /// Returns the terminal outcome to alert on, or null when no new alert is warranted:
    /// nothing to do when the analysis is still pending, or when it already alerted on this
    /// (or any terminal) outcome.
    /// </summary>
    /// <param name="alertedOutcome">The outcome the analysis was last alerted on.</param>
    /// <param name="currentOutcome">The outcome freshly evaluated against the latest candles.</param>
    public static AnalysisOutcome? NewAlert(AnalysisOutcome alertedOutcome, AnalysisOutcome currentOutcome)
    {
        // Only a still-pending analysis can produce a first alert; once terminal it is settled.
        if (alertedOutcome != AnalysisOutcome.Pending)
        {
            return null;
        }

        return currentOutcome is AnalysisOutcome.Invalidated or AnalysisOutcome.TargetReached
            ? currentOutcome
            : null;
    }
}
