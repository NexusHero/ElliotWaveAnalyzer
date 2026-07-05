namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Renders a saved analysis to a publication-grade annotated PNG (candles, zones, invalidation line,
/// scenario arrows, title). Ownership is enforced — a chart is only produced for an analysis the
/// caller owns; otherwise null (the endpoint maps that to 404). The rendering backend stays behind
/// this abstraction so callers never touch SkiaSharp.
/// </summary>
public interface IAnalysisChartService
{
    /// <summary>
    /// Renders the analysis <paramref name="analysisId"/> owned by <paramref name="userId"/> to PNG
    /// bytes, or null when it does not exist or belongs to another user.
    /// </summary>
    Task<byte[]?> RenderChartAsync(Guid userId, Guid analysisId, CancellationToken cancellationToken = default);
}
