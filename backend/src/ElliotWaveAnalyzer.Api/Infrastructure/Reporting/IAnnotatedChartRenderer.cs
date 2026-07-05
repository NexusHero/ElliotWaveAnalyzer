using ElliotWaveAnalyzer.Api.Application.Charting;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Reporting;

/// <summary>
/// Rasterizes a backend-agnostic <see cref="ChartScene"/> to a PNG. Internal to Infrastructure so the
/// SkiaSharp dependency stays confined here (ADR-026); consumers inside Infrastructure depend on this
/// abstraction rather than SkiaSharp directly. Deterministic: the same scene yields byte-identical PNG.
/// </summary>
internal interface IAnnotatedChartRenderer
{
    /// <summary>Renders <paramref name="scene"/> to PNG bytes.</summary>
    byte[] Render(ChartScene scene);
}
