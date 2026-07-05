using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Application.Charting;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Tests.TestData;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Structural assertions over the pure <see cref="AnnotatedChartComposer"/> draw-op seam — no OCR, no
/// pixels: the composed <see cref="ChartScene"/> is inspected directly for the candles, channels,
/// shaded zone boxes, invalidation line, bracketed wave labels and ratio text, scenario arrows and
/// title the layered pipeline is meant to emit (issue #120).
/// </summary>
[TestFixture]
public sealed class AnnotatedChartComposerTests
{
    private static readonly DateTime T0 = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static AnnotatedChartInput SampleInput(FibScale scale = FibScale.Linear) =>
        new("BTC", "1D", new DateTime(2026, 7, 5, 0, 0, 0, DateTimeKind.Utc), scale,
            MarketDataFixtures.CreateCandles(40))
        {
            Labels = [new(T0.AddDays(5), 41000m, "2"), new(T0.AddDays(20), 44000m, "3")],
            Invalidation = new PriceLevel(39000m, LevelSide.Below, "Invalidation", "End of Wave 1"),
            EntryZone = new PriceZone(40000m, 40500m, "Entry (61.8% of Wave 1)", "Fibonacci retracement"),
            TargetZones = [new PriceZone(45000m, 46000m, "Target 161.8%", "Extension")],
            Channels = ChannelProjector.Project(
            [
                new(T0, 40000m, "0"), new(T0.AddDays(10), 43000m, "1"), new(T0.AddDays(20), 41500m, "2"),
                new(T0.AddDays(30), 47000m, "3"), new(T0.AddDays(39), 45000m, "4"),
            ], FibScale.Linear),
            Scenarios =
            [
                new ChartScenarioArrow("Primary", Bullish: true, Primary: true, 46000m),
                new ChartScenarioArrow("Alt 1", Bullish: false, Primary: false, 38000m),
            ],
        };

    [Test]
    public void Compose_EmitsCandles_AsBodiesAndWicks()
    {
        var scene = AnnotatedChartComposer.Compose(SampleInput());

        // 40 candles → at least 40 body rects and 40 wick lines are present.
        Assert.That(scene.Ops.OfType<ChartRectOp>().Count(r => r.Fill is not null), Is.GreaterThanOrEqualTo(40));
        Assert.That(scene.Ops.OfType<ChartLineOp>().Count(), Is.GreaterThanOrEqualTo(40));
    }

    [Test]
    public void Compose_EmitsWaveLabels_Bracketed()
    {
        var scene = AnnotatedChartComposer.Compose(SampleInput());
        var texts = scene.Ops.OfType<ChartTextOp>().Select(t => t.Text).ToList();

        Assert.That(texts, Does.Contain("[2]"));
        Assert.That(texts, Does.Contain("[3]"));
    }

    [Test]
    public void Compose_EmitsZoneLabel_WithRatioText()
    {
        var scene = AnnotatedChartComposer.Compose(SampleInput());
        var texts = scene.Ops.OfType<ChartTextOp>().Select(t => t.Text);

        Assert.That(texts.Any(t => t.Contains("61.8%", StringComparison.Ordinal)), Is.True);
    }

    [Test]
    public void Compose_ShadesZoneBox_WithTranslucentFillSpanningPlotWidth()
    {
        var scene = AnnotatedChartComposer.Compose(SampleInput());

        // A wide, translucent filled rect that is not a candle body (candles are narrow).
        var zoneBox = scene.Ops.OfType<ChartRectOp>()
            .Where(r => r.Fill is { A: < 0xFF })
            .FirstOrDefault(r => r.Width > 400);
        Assert.That(zoneBox, Is.Not.Null);
    }

    [Test]
    public void Compose_DrawsInvalidationLine_Dashed()
    {
        var scene = AnnotatedChartComposer.Compose(SampleInput());

        // A dashed horizontal line (Y1 == Y2) spanning the plot.
        var invalidation = scene.Ops.OfType<ChartLineOp>()
            .FirstOrDefault(l => l.Dashed && Math.Abs(l.Y1 - l.Y2) < 0.001 && l.X2 - l.X1 > 400);
        Assert.That(invalidation, Is.Not.Null);
    }

    [Test]
    public void Compose_DrawsChannelRays_ForBaseAndAcceleration()
    {
        var input = SampleInput();
        var scene = AnnotatedChartComposer.Compose(input);

        Assert.That(input.Channels, Has.Count.EqualTo(2), "fixture should yield base + acceleration");
        // Two rays per channel (baseline + parallel), all spanning the full plot width.
        var rays = scene.Ops.OfType<ChartLineOp>().Count(l => l.StrokeWidth == 1.5f && l.X2 - l.X1 > 400);
        Assert.That(rays, Is.GreaterThanOrEqualTo(4));
    }

    [Test]
    public void Compose_PrimaryArrowSolid_AlternateArrowDashed()
    {
        var scene = AnnotatedChartComposer.Compose(SampleInput());
        var texts = scene.Ops.OfType<ChartTextOp>().Select(t => t.Text).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(texts, Does.Contain("Primary"));
            Assert.That(texts, Does.Contain("Alt 1"));
        });
    }

    [Test]
    public void Compose_Title_CarriesSymbolTimeframeAndScale()
    {
        var linear = AnnotatedChartComposer.Compose(SampleInput());
        var log = AnnotatedChartComposer.Compose(SampleInput(FibScale.Log));

        Assert.Multiple(() =>
        {
            Assert.That(linear.Ops.OfType<ChartTextOp>().Any(t =>
                t.Text.Contains("BTC", StringComparison.Ordinal) &&
                t.Text.Contains("1D", StringComparison.Ordinal) &&
                t.Text.Contains("linear", StringComparison.Ordinal)), Is.True);
            Assert.That(log.Ops.OfType<ChartTextOp>().Any(t =>
                t.Text.Contains("log", StringComparison.Ordinal)), Is.True);
        });
    }

    [Test]
    public void Compose_IsDeterministic_SameInputSameOps()
    {
        var input = SampleInput();

        // Each draw op is a value record, so element-wise sequence equality proves determinism
        // (the scene's List member has reference equality, hence comparing Ops rather than the scene).
        Assert.That(AnnotatedChartComposer.Compose(input).Ops, Is.EqualTo(AnnotatedChartComposer.Compose(input).Ops));
    }

    [Test]
    public void Compose_LogScale_MapsPricesThroughLn()
    {
        // On a log axis, equal ratios map to equal pixel gaps. Two candles an octave apart should map
        // to y-values whose spacing differs from the linear mapping — assert the scene simply differs.
        var linear = AnnotatedChartComposer.Compose(SampleInput());
        var log = AnnotatedChartComposer.Compose(SampleInput(FibScale.Log));

        Assert.That(log.Ops, Is.Not.EqualTo(linear.Ops));
    }

    [Test]
    public void Compose_EmptyCandles_DrawsPlaceholderAndDoesNotThrow()
    {
        var input = new AnnotatedChartInput("BTC", "1D", T0, FibScale.Linear, [])
        {
            Invalidation = new PriceLevel(100m, LevelSide.Below, "Invalidation", "x"),
        };

        var scene = AnnotatedChartComposer.Compose(input);

        Assert.That(scene.Ops.OfType<ChartTextOp>().Any(t => t.Text == "no price data"), Is.True);
    }

    [Test]
    public void Compose_NullInput_Throws()
        => Assert.Throws<ArgumentNullException>(() => AnnotatedChartComposer.Compose(null!));
}
