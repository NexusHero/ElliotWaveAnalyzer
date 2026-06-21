using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using SkiaSharp;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Reporting;

/// <summary>
/// Renders a <see cref="TechnicalAnalysisResult"/> to a dark-themed PNG: a candlestick
/// price pane, an RSI sub-pane (with 30/70 guides), and a MACD histogram sub-pane.
/// Pure given its input (no I/O, no state) so it is straightforward to test.
/// </summary>
public sealed class SkiaSharpChartRenderer : IChartRenderer
{
    private const int Width = 1000;
    private const int Height = 700;
    private const float Left = 55f;
    private const float Right = Width - 20f;

    private static readonly SKColor Background = new(0x10, 0x14, 0x1A);
    private static readonly SKColor Foreground = new(0xC8, 0xD0, 0xDA);
    private static readonly SKColor Grid = new(0x2A, 0x31, 0x3C);
    private static readonly SKColor Bull = new(0x26, 0xA6, 0x9A);
    private static readonly SKColor Bear = new(0xEF, 0x53, 0x50);
    private static readonly SKColor Accent = new(0x42, 0xA5, 0xF5);

    /// <inheritdoc/>
    public byte[] RenderPng(TechnicalAnalysisResult analysis)
    {
        using var bitmap = new SKBitmap(Width, Height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(Background);

        using var font = new SKFont { Size = 18 };
        using var textPaint = new SKPaint { Color = Foreground, IsAntialias = true };
        canvas.DrawText(
            $"{analysis.Symbol}/USD — daily ({analysis.Candles.Count} candles)",
            Left, 28f, SKTextAlign.Left, font, textPaint);

        if (analysis.Candles.Count > 0)
        {
            DrawCandles(canvas, analysis.Candles, top: 45f, bottom: 400f);
            DrawRsi(canvas, analysis.Rsi, top: 435f, bottom: 540f);
            DrawMacd(canvas, analysis.Macd, top: 575f, bottom: 680f);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void DrawCandles(SKCanvas canvas, IReadOnlyList<MarketCandle> candles, float top, float bottom)
    {
        var min = (float)candles.Min(c => c.Low);
        var max = (float)candles.Max(c => c.High);
        DrawPaneFrame(canvas, top, bottom, "Price");

        var step = (Right - Left) / candles.Count;
        var bodyWidth = Math.Max(1f, step * 0.6f);

        for (var i = 0; i < candles.Count; i++)
        {
            var c = candles[i];
            var x = Left + (step * i) + (step / 2f);
            var color = c.Close >= c.Open ? Bull : Bear;
            using var paint = new SKPaint { Color = color, IsAntialias = true, StrokeWidth = 1f };

            var highY = MapY((float)c.High, min, max, top, bottom);
            var lowY = MapY((float)c.Low, min, max, top, bottom);
            canvas.DrawLine(x, highY, x, lowY, paint);

            var openY = MapY((float)c.Open, min, max, top, bottom);
            var closeY = MapY((float)c.Close, min, max, top, bottom);
            var bodyTop = Math.Min(openY, closeY);
            var bodyHeight = Math.Max(1f, Math.Abs(openY - closeY));
            canvas.DrawRect(x - (bodyWidth / 2f), bodyTop, bodyWidth, bodyHeight, paint);
        }
    }

    private static void DrawRsi(SKCanvas canvas, IReadOnlyList<RsiResult> rsi, float top, float bottom)
    {
        DrawPaneFrame(canvas, top, bottom, "RSI");

        // 30 / 70 guide lines.
        using var guide = new SKPaint { Color = Grid, IsAntialias = true, StrokeWidth = 1f };
        canvas.DrawLine(Left, MapY(70f, 0f, 100f, top, bottom), Right, MapY(70f, 0f, 100f, top, bottom), guide);
        canvas.DrawLine(Left, MapY(30f, 0f, 100f, top, bottom), Right, MapY(30f, 0f, 100f, top, bottom), guide);

        DrawSeries(canvas, [.. rsi.Select(r => r.Value)], Accent, top, bottom, 0f, 100f);
    }

    private static void DrawMacd(SKCanvas canvas, IReadOnlyList<MacdResult> macd, float top, float bottom)
    {
        DrawPaneFrame(canvas, top, bottom, "MACD");

        var values = macd.Select(m => m.Histogram).ToList();
        var maxAbs = values.Where(v => v.HasValue).Select(v => Math.Abs((float)v!.Value)).DefaultIfEmpty(1f).Max();
        if (maxAbs <= 0f)
        {
            maxAbs = 1f;
        }

        var zeroY = MapY(0f, -maxAbs, maxAbs, top, bottom);
        var step = (Right - Left) / Math.Max(values.Count, 1);
        var barWidth = Math.Max(1f, step * 0.6f);

        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] is not { } h)
            {
                continue;
            }

            var x = Left + (step * i) + (step / 2f);
            var y = MapY((float)h, -maxAbs, maxAbs, top, bottom);
            var color = h >= 0 ? Bull : Bear;
            using var paint = new SKPaint { Color = color, IsAntialias = true };
            canvas.DrawRect(x - (barWidth / 2f), Math.Min(y, zeroY), barWidth, Math.Max(1f, Math.Abs(y - zeroY)), paint);
        }
    }

    private static void DrawSeries(
        SKCanvas canvas, IReadOnlyList<decimal?> values, SKColor color, float top, float bottom, float min, float max)
    {
        using var paint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
        var step = (Right - Left) / Math.Max(values.Count, 1);

        SKPoint? previous = null;
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] is not { } v)
            {
                previous = null;
                continue;
            }

            var point = new SKPoint(Left + (step * i) + (step / 2f), MapY((float)v, min, max, top, bottom));
            if (previous is { } p)
            {
                canvas.DrawLine(p, point, paint);
            }

            previous = point;
        }
    }

    private static void DrawPaneFrame(SKCanvas canvas, float top, float bottom, string label)
    {
        using var border = new SKPaint { Color = Grid, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
        canvas.DrawRect(Left, top, Right - Left, bottom - top, border);

        using var font = new SKFont { Size = 13 };
        using var text = new SKPaint { Color = Foreground, IsAntialias = true };
        canvas.DrawText(label, Left + 4f, top + 15f, SKTextAlign.Left, font, text);
    }

    private static float MapY(float value, float min, float max, float top, float bottom)
    {
        if (max <= min)
        {
            return (top + bottom) / 2f;
        }

        return bottom - ((value - min) / (max - min) * (bottom - top));
    }
}
