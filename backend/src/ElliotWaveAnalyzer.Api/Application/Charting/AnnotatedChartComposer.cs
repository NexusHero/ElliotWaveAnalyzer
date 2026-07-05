using System.Globalization;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application.Charting;

/// <summary>
/// Lays out a publication-grade annotated Elliott chart as an ordered list of backend-agnostic
/// <see cref="ChartDrawOp"/>s — pure geometry, no rendering backend, no clock, no randomness. The
/// draw order is the layered pipeline from issue #120: background grid → candles → channels → zones →
/// invalidation → wave labels → scenario arrows → title. Because every coordinate is computed here
/// and the output carries no timestamps, the same <see cref="AnnotatedChartInput"/> always yields an
/// identical <see cref="ChartScene"/>; the SkiaSharp backend only replays it (ADR-026).
/// </summary>
public static class AnnotatedChartComposer
{
    private static readonly ChartColor Background = new(0x10, 0x14, 0x1A);
    private static readonly ChartColor Foreground = new(0xC8, 0xD0, 0xDA);
    private static readonly ChartColor Muted = new(0x7A, 0x86, 0x94);
    private static readonly ChartColor Grid = new(0x2A, 0x31, 0x3C);
    private static readonly ChartColor Bull = new(0x26, 0xA6, 0x9A);
    private static readonly ChartColor Bear = new(0xEF, 0x53, 0x50);
    private static readonly ChartColor BaseChannel = new(0x42, 0xA5, 0xF5);
    private static readonly ChartColor AccelChannel = new(0xAB, 0x47, 0xBC);
    private static readonly ChartColor EntryFill = new(0x42, 0xA5, 0xF5, 0x33);
    private static readonly ChartColor TargetFill = new(0x66, 0xBB, 0x6A, 0x33);
    private static readonly ChartColor Invalidation = new(0xEF, 0x53, 0x50);
    private static readonly ChartColor Label = new(0xFF, 0xCA, 0x28);

    private const float PlotLeft = 60f;
    private const float PlotTop = 64f;
    private const float RightMargin = 150f;
    private const float BottomMargin = 44f;
    private const int GridRows = 6;

    /// <summary>Composes the scene for <paramref name="input"/>. Never throws for empty candles.</summary>
    public static ChartScene Compose(AnnotatedChartInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var ops = new List<ChartDrawOp>();
        var plot = new PlotArea(
            PlotLeft, PlotTop, input.Width - RightMargin, input.Height - BottomMargin,
            input.Scale, PriceRange(input));
        if (input.Candles.Count > 0)
        {
            plot.StartDate = input.Candles[0].OpenTime;
            plot.EndDate = input.Candles[^1].OpenTime;
        }

        DrawGrid(ops, plot);
        DrawCandles(ops, plot, input.Candles);
        DrawChannels(ops, plot, input.Channels);
        DrawZone(ops, plot, input.EntryZone, EntryFill);
        foreach (var target in input.TargetZones)
        {
            DrawZone(ops, plot, target, TargetFill);
        }

        DrawInvalidation(ops, plot, input.Invalidation);
        DrawLabels(ops, plot, input.Labels);
        DrawScenarios(ops, plot, input.Candles, input.Scenarios);
        DrawTitle(ops, input);

        if (input.Candles.Count == 0)
        {
            ops.Add(new ChartTextOp(
                (plot.Left + plot.Right) / 2f, (plot.Top + plot.Bottom) / 2f,
                "no price data", Muted, 18f, ChartTextAlign.Center));
        }

        return new ChartScene(input.Width, input.Height, Background, ops);
    }

    /// <summary>The min/max price the axis must span so every drawn element is visible.</summary>
    private static (decimal Min, decimal Max) PriceRange(AnnotatedChartInput input)
    {
        var prices = new List<decimal>();
        foreach (var c in input.Candles)
        {
            prices.Add(c.High);
            prices.Add(c.Low);
        }

        if (input.Invalidation is { } inv)
        {
            prices.Add(inv.Price);
        }

        AddZone(prices, input.EntryZone);
        foreach (var t in input.TargetZones)
        {
            AddZone(prices, t);
        }

        foreach (var a in input.Labels)
        {
            prices.Add(a.Price);
        }

        foreach (var s in input.Scenarios)
        {
            prices.Add(s.TargetPrice);
        }

        var positive = prices.Where(p => p > 0m).ToList();
        if (positive.Count == 0)
        {
            return (1m, 2m);
        }

        var min = positive.Min();
        var max = positive.Max();
        if (max <= min)
        {
            // Single distinct price — open a small symmetric band so the axis has extent.
            max = min * 1.05m;
            min *= 0.95m;
        }

        var pad = (max - min) * 0.06m;
        return (min - pad, max + pad);
    }

    private static void AddZone(List<decimal> prices, PriceZone? zone)
    {
        if (zone is { } z)
        {
            prices.Add(z.Low);
            prices.Add(z.High);
        }
    }

    private static void DrawGrid(List<ChartDrawOp> ops, PlotArea plot)
    {
        ops.Add(new ChartRectOp(plot.Left, plot.Top, plot.Width, plot.Height, Fill: null, Grid));
        for (var r = 1; r < GridRows; r++)
        {
            var y = plot.Top + (plot.Height * r / GridRows);
            ops.Add(new ChartLineOp(plot.Left, y, plot.Right, y, Grid));
        }
    }

    private static void DrawCandles(List<ChartDrawOp> ops, PlotArea plot, IReadOnlyList<MarketCandle> candles)
    {
        if (candles.Count == 0)
        {
            return;
        }

        var step = plot.Width / candles.Count;
        var bodyWidth = Math.Max(1.0, step * 0.6);
        for (var i = 0; i < candles.Count; i++)
        {
            var c = candles[i];
            var x = plot.Left + (step * i) + (step / 2.0);
            var color = c.Close >= c.Open ? Bull : Bear;

            ops.Add(new ChartLineOp(x, plot.MapY(c.High), x, plot.MapY(c.Low), color));

            var openY = plot.MapY(c.Open);
            var closeY = plot.MapY(c.Close);
            var top = Math.Min(openY, closeY);
            ops.Add(new ChartRectOp(
                x - (bodyWidth / 2.0), top, bodyWidth, Math.Max(1.0, Math.Abs(openY - closeY)), color));
        }
    }

    private static void DrawChannels(List<ChartDrawOp> ops, PlotArea plot, IReadOnlyList<Channel> channels)
    {
        foreach (var channel in channels)
        {
            var color = channel.Kind == ChannelKind.Base ? BaseChannel : AccelChannel;
            DrawChannelLine(ops, plot, channel.OriginDate, channel.Baseline, color);
            DrawChannelLine(ops, plot, channel.OriginDate, channel.Parallel, color);
        }
    }

    private static void DrawChannelLine(
        List<ChartDrawOp> ops, PlotArea plot, DateTime origin, ChannelLine line, ChartColor color)
    {
        // Evaluate the line (already in the axis' y-space) at the plot's left and right time edges.
        var y1 = plot.MapAxisY((double)line.ValueAt((decimal)(plot.StartDate - origin).TotalDays));
        var y2 = plot.MapAxisY((double)line.ValueAt((decimal)(plot.EndDate - origin).TotalDays));
        ops.Add(new ChartLineOp(plot.Left, y1, plot.Right, y2, color, StrokeWidth: 1.5f));
    }

    private static void DrawZone(List<ChartDrawOp> ops, PlotArea plot, PriceZone? zone, ChartColor fill)
    {
        if (zone is not { } z)
        {
            return;
        }

        var top = plot.MapY(z.High);
        var bottom = plot.MapY(z.Low);
        ops.Add(new ChartRectOp(
            plot.Left, top, plot.Width, Math.Max(1.0, bottom - top),
            fill, fill.WithAlpha(0xAA)));

        var color = fill.WithAlpha(0xFF);
        ops.Add(new ChartTextOp(plot.Right + 6f, top + 12f, FormatPrice(z.High), color, 12f));
        ops.Add(new ChartTextOp(plot.Right + 6f, bottom - 2f, FormatPrice(z.Low), color, 12f));
        ops.Add(new ChartTextOp(plot.Left + 6f, top + 14f, z.Label, color, 12f));
    }

    private static void DrawInvalidation(List<ChartDrawOp> ops, PlotArea plot, PriceLevel? level)
    {
        if (level is not { } l)
        {
            return;
        }

        var y = plot.MapY(l.Price);
        ops.Add(new ChartLineOp(plot.Left, y, plot.Right, y, Invalidation, StrokeWidth: 2f, Dashed: true));
        ops.Add(new ChartTextOp(
            plot.Right + 6f, y + 4f, $"✕ {FormatPrice(l.Price)}", Invalidation, 13f));
    }

    private static void DrawLabels(List<ChartDrawOp> ops, PlotArea plot, IReadOnlyList<WaveAnnotation> labels)
    {
        foreach (var a in labels)
        {
            var x = plot.MapX(a.Date);
            var y = plot.MapY(a.Price);
            ops.Add(new ChartRectOp(x - 2f, y - 2f, 4f, 4f, Label));
            ops.Add(new ChartTextOp(x, y - 8f, $"[{a.Label}]", Label, 15f, ChartTextAlign.Center));
        }
    }

    private static void DrawScenarios(
        List<ChartDrawOp> ops, PlotArea plot, IReadOnlyList<MarketCandle> candles,
        IReadOnlyList<ChartScenarioArrow> scenarios)
    {
        if (candles.Count == 0 || scenarios.Count == 0)
        {
            return;
        }

        var startX = plot.Left + plot.Width * 0.82;
        var tipX = plot.Right - 4f;
        var lastClose = candles[^1].Close;
        foreach (var s in scenarios)
        {
            var color = s.Bullish ? Bull : Bear;
            var y1 = plot.MapY(lastClose);
            var y2 = plot.MapY(s.TargetPrice);
            ops.Add(new ChartLineOp(startX, y1, tipX, y2, color, StrokeWidth: 2f, Dashed: !s.Primary));

            // Arrowhead: two short strokes back from the tip.
            var dir = Math.Sign(y2 - y1);
            ops.Add(new ChartLineOp(tipX, y2, tipX - 8f, y2 - (dir * 6f) - 4f, color, StrokeWidth: 2f));
            ops.Add(new ChartLineOp(tipX, y2, tipX - 8f, y2 - (dir * 6f) + 4f, color, StrokeWidth: 2f));
            ops.Add(new ChartTextOp(tipX, y2 - 6f, s.Label, color, 12f, ChartTextAlign.Right));
        }
    }

    private static void DrawTitle(List<ChartDrawOp> ops, AnnotatedChartInput input)
    {
        var scale = input.Scale == FibScale.Log ? "log" : "linear";
        var date = input.RenderDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        ops.Add(new ChartTextOp(
            PlotLeft, 34f,
            $"{input.Symbol} · {input.Timeframe} · {date} · {scale} scale",
            Foreground, 20f));
    }

    private static string FormatPrice(decimal price)
        => price.ToString(price >= 1000m ? "N0" : "0.####", CultureInfo.InvariantCulture);

    /// <summary>
    /// Maps price/date to pixel coordinates for one plot rectangle. Y is linear in price on a linear
    /// axis and linear in ln(price) on a log axis; X is linear in candle index, with dates located by
    /// interpolation against the candle time span.
    /// </summary>
    private sealed class PlotArea
    {
        private readonly double _yMin;
        private readonly double _yMax;
        private readonly bool _log;

        public PlotArea(double left, double top, double right, double bottom, FibScale scale, (decimal Min, decimal Max) range)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
            _log = scale == FibScale.Log;
            _yMin = ToAxis((double)range.Min);
            _yMax = ToAxis((double)range.Max);
            if (_yMax <= _yMin)
            {
                _yMax = _yMin + 1.0;
            }
        }

        public double Left { get; }
        public double Top { get; }
        public double Right { get; }
        public double Bottom { get; }
        public double Width => Right - Left;
        public double Height => Bottom - Top;

        /// <summary>First and last candle dates, set by the composer so channels span the plot.</summary>
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public double MapAxisY(double axisY) => Bottom - ((axisY - _yMin) / (_yMax - _yMin) * (Bottom - Top));

        public double MapY(decimal price) => MapAxisY(ToAxis((double)price));

        public double MapX(DateTime date)
        {
            if (EndDate <= StartDate)
            {
                return Left + (Width / 2.0);
            }

            var t = (date - StartDate).TotalSeconds / (EndDate - StartDate).TotalSeconds;
            t = Math.Clamp(t, 0.0, 1.0);
            return Left + (t * Width);
        }

        private double ToAxis(double price)
        {
            if (!_log)
            {
                return price;
            }

            return price <= 0.0 ? 0.0 : Math.Log(price);
        }
    }
}
