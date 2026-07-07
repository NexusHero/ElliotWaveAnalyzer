using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Pure computation of the "miss set" a reflection lesson can be drawn from (#189, AC1): concluded
/// tracked analyses that invalidated rather than reaching their target. Pending analyses are excluded
/// by construction — an unsettled call is not yet a miss (or a hit) — and <see cref="AnalysisOutcome.TargetReached"/>
/// analyses are excluded too, since they are not misses. Deterministic: the same analyses always
/// produce the same miss set. No I/O, no LLM.
/// </summary>
public static class MissSetCalculator
{
    /// <summary>Computes the miss set from a track record's tracked analyses.</summary>
    public static IReadOnlyList<MissCase> Compute(IReadOnlyList<TrackedAnalysis> analyses)
    {
        ArgumentNullException.ThrowIfNull(analyses);

        return analyses
            .Where(a => a.Outcome == AnalysisOutcome.Invalidated)
            .Select(a => new MissCase(a.Id, a.Symbol, a.Structure, NormalizeConfidence(a.Confidence), a.CreatedAt))
            .ToList();
    }

    private static string NormalizeConfidence(string confidence)
        => string.IsNullOrWhiteSpace(confidence) ? "unknown" : confidence.Trim().ToLowerInvariant();
}
