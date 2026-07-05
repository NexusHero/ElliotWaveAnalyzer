using CsCheck;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Properties;

/// <summary>
/// CsCheck generators for the property suites (ADR-034): they emit only <b>valid</b> fixtures — OHLC
/// candles with <c>Low ≤ Open,Close ≤ High</c> and annotations that sit on real candle extremes — so a
/// property failure means the code under test is wrong, never the fixture. Reused across the risk,
/// verifier, snapper and metamorphic suites.
/// </summary>
internal static class PropertyGenerators
{
    internal static readonly DateTime T0 = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // Valid Elliott labels; the origin is labelled "1" onward (labels only drive structure inference).
    private static readonly string[] Labels = ["1", "2", "3", "4", "5", "A", "B", "C"];

    /// <summary>A single valid OHLC candle from a low, a range, and open/close fractions within it.</summary>
    private static MarketCandle Candle(DateTime date, double low, double range, double openFrac, double closeFrac)
    {
        var l = (decimal)low;
        var high = l + (decimal)range;
        var open = l + (decimal)(range * openFrac);
        var close = l + (decimal)(range * closeFrac);
        return new MarketCandle(date, open, high, l, close, 0m);
    }

    /// <summary>6–20 valid candles on consecutive days.</summary>
    internal static readonly Gen<MarketCandle[]> Candles =
        Gen.Select(Gen.Double[10.0, 1000.0], Gen.Double[0.5, 200.0], Gen.Double[0.0, 1.0], Gen.Double[0.0, 1.0],
                (low, range, openFrac, closeFrac) => (low, range, openFrac, closeFrac))
            .Array[6, 20]
            .Select(shapes => shapes
                .Select((s, i) => Candle(T0.AddDays(i), s.low, s.range, s.openFrac, s.closeFrac))
                .ToArray());

    /// <summary>
    /// A scenario: valid candles plus 2–6 annotations placed on the first candles' real extremes
    /// (alternating high/low), so every annotation snaps to real data.
    /// </summary>
    internal static readonly Gen<WaveScenario> Scenarios =
        Candles.SelectMany(candles =>
            Gen.Int[2, Math.Min(candles.Length, 6)].Select(k =>
            {
                var annotations = new List<WaveAnnotation>(k);
                for (var i = 0; i < k; i++)
                {
                    var c = candles[i];
                    var price = i % 2 == 0 ? c.High : c.Low; // alternate extremes for a swinging count
                    annotations.Add(new WaveAnnotation(c.OpenTime, price, Labels[i % Labels.Length]));
                }

                return new WaveScenario(candles, annotations);
            }));
}
