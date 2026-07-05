using System.Security.Cryptography;
using ElliotWaveAnalyzer.Api.Application.Charting;
using ElliotWaveAnalyzer.Api.Infrastructure.Reporting;
using SkiaSharp;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Tests for the SkiaSharp draw-op backend: byte-identical output for the same scene (determinism —
/// ADR-026) and structural pixel assertions decoded from the PNG (a filled rect paints its fill colour
/// inside its rectangle and not outside; a horizontal line paints a run of its colour at its row).
/// </summary>
[TestFixture]
public sealed class SkiaAnnotatedChartRendererTests
{
    private readonly SkiaAnnotatedChartRenderer _sut = new();

    private static readonly ChartColor Bg = new(0x10, 0x14, 0x1A);
    private static readonly ChartColor ZoneFill = new(0x66, 0xBB, 0x6A);
    private static readonly ChartColor LineColor = new(0xEF, 0x53, 0x50);

    [Test]
    public void Render_SameScene_ProducesByteIdenticalPng()
    {
        var scene = SampleScene();

        var a = SHA256.HashData(_sut.Render(scene));
        var b = SHA256.HashData(_sut.Render(scene));

        Assert.That(a, Is.EqualTo(b));
    }

    [Test]
    public void Render_FilledRect_PaintsFillInsideButNotOutside()
    {
        var scene = new ChartScene(200, 200, Bg,
            [new ChartRectOp(50, 50, 100, 100, ZoneFill)]);

        using var bitmap = SKBitmap.Decode(_sut.Render(scene));

        Assert.Multiple(() =>
        {
            var inside = bitmap.GetPixel(100, 100);
            Assert.That((inside.Red, inside.Green, inside.Blue), Is.EqualTo((ZoneFill.R, ZoneFill.G, ZoneFill.B)));

            var outside = bitmap.GetPixel(10, 10);
            Assert.That((outside.Red, outside.Green, outside.Blue), Is.EqualTo((Bg.R, Bg.G, Bg.B)));
        });
    }

    [Test]
    public void Render_HorizontalLine_PaintsColourRunAtItsRow()
    {
        var scene = new ChartScene(200, 200, Bg,
            [new ChartLineOp(20, 100, 180, 100, LineColor, StrokeWidth: 3f)]);

        using var bitmap = SKBitmap.Decode(_sut.Render(scene));
        var pixel = bitmap.GetPixel(100, 100);

        Assert.That((pixel.Red, pixel.Green, pixel.Blue), Is.EqualTo((LineColor.R, LineColor.G, LineColor.B)));
    }

    [Test]
    public void Render_RendersText_WithoutThrowing_AndProducesNonTrivialPng()
    {
        var scene = new ChartScene(400, 200, Bg,
        [
            new ChartTextOp(20, 40, "BTC · 1D · log scale", new ChartColor(0xC8, 0xD0, 0xDA), 18f),
            new ChartTextOp(20, 80, "[3]", new ChartColor(0xFF, 0xCA, 0x28), 15f, ChartTextAlign.Center),
        ]);

        var png = _sut.Render(scene);

        Assert.That(png, Is.Not.Empty);
        Assert.That(png.Length, Is.GreaterThan(100));
    }

    [Test]
    public void Render_NullScene_Throws()
        => Assert.Throws<ArgumentNullException>(() => _sut.Render(null!));

    private static ChartScene SampleScene() => new(300, 200, Bg,
    [
        new ChartRectOp(10, 10, 280, 180, Fill: null, Stroke: new ChartColor(0x2A, 0x31, 0x3C)),
        new ChartRectOp(40, 40, 120, 60, ZoneFill.WithAlpha(0x33), ZoneFill),
        new ChartLineOp(10, 100, 290, 100, LineColor, StrokeWidth: 2f, Dashed: true),
        new ChartTextOp(20, 30, "title", new ChartColor(0xC8, 0xD0, 0xDA), 14f),
    ]);
}
