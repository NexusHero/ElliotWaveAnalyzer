using ElliotWaveAnalyzer.Api.Application.Charting;
using ElliotWaveAnalyzer.Api.Domain;

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
    /// bytes, or null when it does not exist or belongs to another user. <paramref name="theme"/>,
    /// <paramref name="scale"/>, <paramref name="scale2x"/> and <paramref name="watermarkText"/> are
    /// the #227 export options; every one defaults to the pre-#227 look so an existing caller that
    /// passes none of them keeps getting a valid PNG (#227 AC4).
    /// </summary>
    Task<byte[]?> RenderChartAsync(
        Guid userId,
        Guid analysisId,
        ChartTheme theme = ChartTheme.Dark,
        FibScale scale = FibScale.Linear,
        bool scale2x = false,
        string? watermarkText = null,
        CancellationToken cancellationToken = default);
}
