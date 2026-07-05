namespace ElliotWaveAnalyzer.Api.Application.Charting;

/// <summary>
/// A fully-laid-out, backend-agnostic chart: a canvas size, a background colour and the ordered list
/// of <see cref="ChartDrawOp"/>s to replay (already mapped to pixel space). Produced by
/// <see cref="AnnotatedChartComposer"/> and consumed by a rendering backend. Because it carries no
/// timestamps or randomness, the same input yields an identical scene — the basis for the renderer's
/// deterministic-output guarantee.
/// </summary>
/// <param name="Width">Canvas width in pixels.</param>
/// <param name="Height">Canvas height in pixels.</param>
/// <param name="Background">Canvas clear colour.</param>
/// <param name="Ops">Draw operations in back-to-front order.</param>
public sealed record ChartScene(
    int Width,
    int Height,
    ChartColor Background,
    IReadOnlyList<ChartDrawOp> Ops);
