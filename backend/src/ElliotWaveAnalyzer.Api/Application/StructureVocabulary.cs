using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// The bounded Elliott structure vocabulary the LLM may propose from. A proposal is mapped to a known
/// <see cref="StructureKind"/> the engine can actually generate and rule-check; anything outside the
/// vocabulary (a made-up structure, or one this engine doesn't model yet) is rejected here — before it
/// ever reaches generation. This is the guard that keeps the LLM proposing only testable hypotheses.
/// </summary>
public static class StructureVocabulary
{
    /// <summary>
    /// Maps a proposed structure name to a known <see cref="StructureKind"/>, or null when it is
    /// outside the vocabulary. Case-insensitive and tolerant of qualifiers ("leading diagonal",
    /// "expanded flat") since the engine models the family, not the sub-variant.
    /// </summary>
    public static StructureKind? TryParse(string? proposal)
    {
        if (string.IsNullOrWhiteSpace(proposal))
        {
            return null;
        }

        var text = proposal.Trim().ToLowerInvariant();

        // Order matters: check the more specific families before the motive fallback.
        if (text.Contains("diagonal")) return StructureKind.Diagonal;
        if (text.Contains("zigzag") || text.Contains("zig-zag")) return StructureKind.Zigzag;
        if (text.Contains("flat")) return StructureKind.Flat;
        if (text.Contains("triangle")) return StructureKind.Triangle;
        if (text.Contains("impulse")) return StructureKind.Impulse;

        // Combinations, double/triple threes, "wave X", etc. are real Elliott but not modelled by this
        // engine's rule checkers, so they are out of vocabulary rather than silently mis-validated.
        return null;
    }
}
