using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure;
using ElliotWaveAnalyzer.Api.Infrastructure.Reporting;
using ElliotWaveAnalyzer.Tests.TestData;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="SkiaSharpChartRenderer"/>. Verifies it produces a valid PNG
/// for both populated and empty inputs (no crash on edge cases).
/// </summary>
[TestFixture]
public sealed class SkiaSharpChartRendererTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];

    private static TechnicalAnalysisResult BuildAnalysis(int candleCount)
    {
        var candles = MarketDataFixtures.CreateCandles(candleCount);
        var calculator = new SkenderIndicatorCalculator();
        return new TechnicalAnalysisResult(
            "BTC", candles, calculator.CalculateMacd(candles), calculator.CalculateRsi(candles));
    }

    [Test]
    public void RenderPng_WithData_ReturnsNonEmptyPng()
    {
        var renderer = new SkiaSharpChartRenderer();

        var png = renderer.RenderPng(BuildAnalysis(60));

        Assert.That(png, Is.Not.Empty);
        Assert.That(png[..4], Is.EqualTo(PngSignature), "output should start with the PNG signature");
    }

    [Test]
    public void RenderPng_NoCandles_StillReturnsValidPng()
    {
        var renderer = new SkiaSharpChartRenderer();

        var png = renderer.RenderPng(new TechnicalAnalysisResult("BTC", [], [], []));

        Assert.That(png, Is.Not.Empty);
        Assert.That(png[..4], Is.EqualTo(PngSignature));
    }
}
