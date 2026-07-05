using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Writes a short natural-language narrative for a position <b>strictly from the deterministic facts</b>
/// in its brief — the same discipline as the auto-analysis ranker: the model narrates, it never invents
/// a number. Degrades gracefully: when no LLM key is configured, or the model's text fails the
/// fact-guard, it returns an explicit <see cref="PositionNarration.UnavailableReason"/> rather than a
/// fabricated or empty narrative.
/// </summary>
public interface IPositionNarrator
{
    /// <summary>Narrates <paramref name="brief"/>, or returns an unavailable reason.</summary>
    Task<PositionNarration> NarrateAsync(PositionBrief brief, CancellationToken cancellationToken = default);
}
