using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Attaches a short, grounded natural-language summary to a deterministic <see cref="SentimentReport"/>.
/// The narrator may only contrast the mood series/divergences in prose — every figure it cites is
/// checked against the computed report (<see cref="Application.SentimentFactGuard"/>). Implementations
/// degrade gracefully: with no LLM key, no sentiment coverage, or a transport/guard failure they
/// return the report unchanged with a <see cref="SentimentReport.NarrativeUnavailableReason"/> set, so
/// the deterministic read always stands.
/// </summary>
public interface ISentimentNarrator
{
    /// <summary>Returns the report with a fact-guarded narrative attached, or a reason it is absent.</summary>
    Task<SentimentReport> NarrateAsync(SentimentReport report, CancellationToken cancellationToken = default);
}
