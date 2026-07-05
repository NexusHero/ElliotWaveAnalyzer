namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>The count that would apply if the primary count's invalidation breaks.</summary>
/// <param name="Name">Short name, e.g. "Ending diagonal / ABC".</param>
/// <param name="Note">One-line explanation of when and why it takes over.</param>
public sealed record AlternativeScenario(string Name, string Note);
