namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The outcome of narrating a position: either a fact-checked narrative, or an explicit reason it is
/// unavailable (no LLM key configured, or the model's text failed the fact-guard). Never both.
/// </summary>
/// <param name="Narrative">The fact-derived narrative, or null when unavailable.</param>
/// <param name="UnavailableReason">Why the narrative is absent, or null when present.</param>
public sealed record PositionNarration(string? Narrative, string? UnavailableReason)
{
    /// <summary>A successful narration.</summary>
    public static PositionNarration Of(string narrative) => new(narrative, null);

    /// <summary>An unavailable narration with a reason.</summary>
    public static PositionNarration Unavailable(string reason) => new(null, reason);
}
