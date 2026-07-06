namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>The count that would apply if the primary count's invalidation breaks.</summary>
/// <param name="Name">Short name, e.g. "Ending diagonal / ABC".</param>
/// <param name="Note">One-line explanation of when and why it takes over.</param>
/// <param name="Reinterpretation">
/// The re-projectable reading behind the name — the same pivots re-read under the opposite mode,
/// so the alternative can be drawn as a real projection (its own zones/invalidation), not just a
/// label. Null for legacy/None alternatives. Resolved lazily via
/// <see cref="Application.ProjectionService.Resolve"/>.
/// </param>
public sealed record AlternativeScenario(
    string Name,
    string Note,
    ScenarioReinterpretation? Reinterpretation = null);
