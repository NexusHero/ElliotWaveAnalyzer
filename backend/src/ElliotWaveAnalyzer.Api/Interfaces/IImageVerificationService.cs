using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Verifies an uploaded analyst chart end to end: extract the claimed count (vision), fetch the real
/// candles for the instrument/timeframe, snap the claimed pivots to real extremes (hallucination
/// guard), run the deterministic rules on what survives, and compare with our own count. LLM for
/// perception, rules for judgment.
/// </summary>
public interface IImageVerificationService
{
    /// <summary>
    /// Verifies <paramref name="image"/>. <paramref name="symbol"/>/<paramref name="timeframe"/> override
    /// whatever the vision model read from the image; when both they and the extraction are empty, the
    /// instrument can't be determined and an <see cref="ArgumentException"/> is thrown.
    /// </summary>
    Task<ImageVerificationReport> VerifyAsync(
        byte[] image, string contentType, string? symbol, string? timeframe, CancellationToken cancellationToken = default);
}
