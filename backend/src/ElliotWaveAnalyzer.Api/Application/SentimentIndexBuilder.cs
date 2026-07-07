using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Normalizes a sentiment provider's raw readings into the reproducible index the rest of the
/// socionomics feature reads (mirrors an <see cref="Interfaces.IIndicatorCalculator"/>-style pure
/// transform): clamped to [-1, 1] and sorted by date. Pure and deterministic — identical input
/// always yields an identical output (AC1) — so a provider's scale/outliers can never leak into the
/// divergence math or the LLM fact-guard as an out-of-range figure.
/// </summary>
public static class SentimentIndexBuilder
{
    /// <summary>Clamps every reading to [-1, 1] and returns them ordered by date.</summary>
    public static IReadOnlyList<SentimentPoint> Normalize(IReadOnlyList<SentimentPoint> raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        return raw
            .Select(p => p with { Score = Math.Clamp(p.Score, -1.0, 1.0) })
            .OrderBy(p => p.Date)
            .ToList();
    }
}
