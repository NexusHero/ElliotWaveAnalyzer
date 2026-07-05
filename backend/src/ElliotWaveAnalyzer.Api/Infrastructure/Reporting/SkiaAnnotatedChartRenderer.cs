using ElliotWaveAnalyzer.Api.Application.Charting;
using SkiaSharp;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Reporting;

/// <summary>
/// SkiaSharp backend for the annotated-chart draw-op seam: replays a <see cref="ChartScene"/>'s
/// primitives onto an offscreen bitmap and encodes PNG. All layout/geometry already happened in
/// <see cref="AnnotatedChartComposer"/> — this class only paints, so it carries no analytical logic
/// (ADR-026). Output is deterministic for a given scene (fixed canvas, no clock, no randomness).
/// </summary>
internal sealed class SkiaAnnotatedChartRenderer : IAnnotatedChartRenderer
{
    private static readonly float[] DashPattern = [6f, 4f];

    /// <inheritdoc/>
    public byte[] Render(ChartScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        using var bitmap = new SKBitmap(scene.Width, scene.Height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(ToSkia(scene.Background));

        foreach (var op in scene.Ops)
        {
            switch (op)
            {
                case ChartLineOp line:
                    DrawLine(canvas, line);
                    break;
                case ChartRectOp rect:
                    DrawRect(canvas, rect);
                    break;
                case ChartTextOp text:
                    DrawText(canvas, text);
                    break;
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void DrawLine(SKCanvas canvas, ChartLineOp line)
    {
        using var paint = new SKPaint
        {
            Color = ToSkia(line.Color),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = line.StrokeWidth,
            PathEffect = line.Dashed ? SKPathEffect.CreateDash(DashPattern, 0f) : null,
        };
        canvas.DrawLine((float)line.X1, (float)line.Y1, (float)line.X2, (float)line.Y2, paint);
    }

    private static void DrawRect(SKCanvas canvas, ChartRectOp rect)
    {
        var skRect = SKRect.Create((float)rect.X, (float)rect.Y, (float)rect.Width, (float)rect.Height);
        if (rect.Fill is { } fill)
        {
            using var fillPaint = new SKPaint { Color = ToSkia(fill), IsAntialias = true, Style = SKPaintStyle.Fill };
            canvas.DrawRect(skRect, fillPaint);
        }

        if (rect.Stroke is { } stroke)
        {
            using var strokePaint = new SKPaint
            {
                Color = ToSkia(stroke),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = rect.StrokeWidth,
            };
            canvas.DrawRect(skRect, strokePaint);
        }
    }

    private static void DrawText(SKCanvas canvas, ChartTextOp text)
    {
        using var font = new SKFont { Size = text.Size };
        using var paint = new SKPaint { Color = ToSkia(text.Color), IsAntialias = true };
        canvas.DrawText(text.Text, (float)text.X, (float)text.Y, ToSkia(text.Align), font, paint);
    }

    private static SKColor ToSkia(ChartColor c) => new(c.R, c.G, c.B, c.A);

    private static SKTextAlign ToSkia(ChartTextAlign align) => align switch
    {
        ChartTextAlign.Center => SKTextAlign.Center,
        ChartTextAlign.Right => SKTextAlign.Right,
        _ => SKTextAlign.Left,
    };
}
