using System.Globalization;
using System.Text.RegularExpressions;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// The anti-hallucination guard for the trade-thesis report (#187, sibling of
/// <see cref="PositionFactGuard"/>/<see cref="AnalogFactGuard"/>/<see cref="SentimentFactGuard"/>): a
/// narrative may only mention <b>price</b> numbers that appear somewhere in the fact sheet — current
/// price, invalidation, entry/target zones, confluence zones, risk entry/stop/targets, and every
/// scenario's own levels. Wave labels and percentages are legitimate prose, not fabricated prices, and
/// are not checked. Pure and static so the guard is exhaustively unit-testable.
/// </summary>
public static partial class ThesisFactGuard
{
    /// <summary>Relative tolerance when matching a mentioned price to a fact price (0.5%).</summary>
    private const decimal Tolerance = 0.005m;

    /// <summary>Numbers at or above this magnitude are treated as prices even without a decimal point.</summary>
    private const decimal PriceMagnitude = 1000m;

    /// <summary>
    /// True when every price-like number in <paramref name="narrative"/> matches one of the fact
    /// sheet's prices. A narrative with no price-like numbers passes trivially.
    /// </summary>
    public static bool Passes(string narrative, ThesisFactSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(narrative);
        ArgumentNullException.ThrowIfNull(sheet);

        var facts = FactPrices(sheet);
        foreach (Match match in NumberPattern().Matches(narrative))
        {
            var raw = match.Value.Replace(",", "", StringComparison.Ordinal);
            if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            // A number written as a percentage (immediately followed by '%') is legitimate prose —
            // a Fibonacci ratio, a hit rate, a move size — not a fabricated price.
            var after = match.Index + match.Length;
            if (after < narrative.Length && narrative[after] == '%')
            {
                continue;
            }

            var isPriceLike = raw.Contains('.', StringComparison.Ordinal) || value >= PriceMagnitude;
            if (isPriceLike && !MatchesAFact(value, facts))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The set of legitimate fact prices a narrative may cite for <paramref name="sheet"/>.</summary>
    public static IReadOnlyList<decimal> FactPrices(ThesisFactSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var prices = new List<decimal>();
        if (sheet.CurrentPrice is { } price)
        {
            prices.Add(price);
        }

        if (sheet.Invalidation is { } inv)
        {
            prices.Add(inv.Price);
        }

        AddZone(prices, sheet.EntryZone);
        foreach (var target in sheet.TargetZones)
        {
            AddZone(prices, target);
        }

        foreach (var zone in sheet.ConfluenceZones)
        {
            prices.Add(zone.Low);
            prices.Add(zone.High);
        }

        if (sheet.Risk is { } risk)
        {
            prices.Add(risk.Entry);
            prices.Add(risk.StopPrice);
            foreach (var target in risk.Targets)
            {
                prices.Add(target.Price);
            }
        }

        foreach (var scenario in sheet.Scenarios)
        {
            AddIfPresent(prices, scenario.InvalidationPrice);
            AddIfPresent(prices, scenario.EntryLow);
            AddIfPresent(prices, scenario.EntryHigh);
            AddIfPresent(prices, scenario.TargetLow);
            AddIfPresent(prices, scenario.TargetHigh);
        }

        return prices;
    }

    private static void AddZone(List<decimal> prices, PriceZone? zone)
    {
        if (zone is { } z)
        {
            prices.Add(z.Low);
            prices.Add(z.High);
        }
    }

    private static void AddIfPresent(List<decimal> prices, decimal? value)
    {
        if (value is { } v)
        {
            prices.Add(v);
        }
    }

    private static bool MatchesAFact(decimal value, IReadOnlyList<decimal> facts)
    {
        foreach (var fact in facts)
        {
            var scale = Math.Max(Math.Abs(fact), 1m);
            if (Math.Abs(value - fact) <= scale * Tolerance)
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"[0-9][0-9,]*(?:\.[0-9]+)?")]
    private static partial Regex NumberPattern();
}
