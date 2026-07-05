namespace ElliotWaveAnalyzer.Api.Application.Charting;

/// <summary>Horizontal anchor of a <see cref="ChartTextOp"/> relative to its (X, Y) point.</summary>
public enum ChartTextAlign
{
    /// <summary>X is the left edge of the text.</summary>
    Left,

    /// <summary>X is the horizontal centre of the text.</summary>
    Center,

    /// <summary>X is the right edge of the text.</summary>
    Right,
}
