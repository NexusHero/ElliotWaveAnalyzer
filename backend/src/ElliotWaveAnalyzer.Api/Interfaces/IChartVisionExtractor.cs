using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Extracts a claimed Elliott Wave count from a chart image using a vision-capable model — perception
/// only. The output is a set of <em>claims</em> (approximate pivots, levels, zones) that a deterministic
/// pipeline verifies against real data; this seam never judges a count. Throws
/// <see cref="ChartExtractionException"/> when the model's output can't be parsed after the allowed retry.
/// </summary>
public interface IChartVisionExtractor
{
    /// <summary>Extracts the claimed count from <paramref name="image"/> (a PNG/JPEG upload).</summary>
    Task<ChartExtraction> ExtractAsync(byte[] image, string contentType, CancellationToken cancellationToken = default);
}
