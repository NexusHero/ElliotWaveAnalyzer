namespace ElliotWaveAnalyzer.Api.Application.Charting;

/// <summary>
/// A run of text anchored at (<paramref name="X"/>, <paramref name="Y"/>) in pixel space, where Y is
/// the text baseline. Used for the title block, wave labels (e.g. <c>[3]</c>), zone edge prices and
/// ratio labels (e.g. <c>61.8%</c>) and the invalidation price tag.
/// </summary>
/// <param name="X">Anchor x (meaning depends on <paramref name="Align"/>).</param>
/// <param name="Y">Text baseline y.</param>
/// <param name="Text">The literal string to draw.</param>
/// <param name="Color">Text colour.</param>
/// <param name="Size">Font size in pixels.</param>
/// <param name="Align">Horizontal anchor.</param>
public sealed record ChartTextOp(
    double X,
    double Y,
    string Text,
    ChartColor Color,
    float Size,
    ChartTextAlign Align = ChartTextAlign.Left) : ChartDrawOp;
