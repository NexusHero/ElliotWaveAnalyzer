using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Renders a technical-analysis result to a chart image. Abstracted so the rendering
/// backend (SkiaSharp today) can be swapped, and so callers can be unit-tested with a fake.
/// </summary>
public interface IChartRenderer
{
    /// <summary>Renders candles + RSI + MACD to a PNG image.</summary>
    byte[] RenderPng(TechnicalAnalysisResult analysis);
}
