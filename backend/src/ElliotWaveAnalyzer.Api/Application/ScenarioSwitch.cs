using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Decides which alternate to promote when the primary scenario's invalidation breaks: the
/// highest-scored surviving alternate (ties broken by probability, then label for determinism).
/// Returns null when no alternate remains — in which case the analysis simply concludes as
/// invalidated. Pure and deterministic.
/// </summary>
public static class ScenarioSwitch
{
    /// <summary>
    /// The best alternate to promote from <paramref name="alternates"/>, or null if there is none.
    /// Retired scenarios are never promoted.
    /// </summary>
    public static Scenario? SelectPromotion(IReadOnlyList<Scenario> alternates)
    {
        ArgumentNullException.ThrowIfNull(alternates);

        return alternates
            .Where(s => s is { Role: ScenarioRole.Alternate, Retired: false })
            .OrderByDescending(s => s.Score ?? 0m)
            .ThenByDescending(s => s.Probability ?? 0m)
            .ThenBy(s => s.Label, StringComparer.Ordinal)
            .FirstOrDefault();
    }
}
