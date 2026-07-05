using System.Globalization;
using System.Text.RegularExpressions;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// The anti-hallucination guard: a narrative may only mention <b>price</b> numbers that appear in the
/// position's deterministic facts. It extracts price-like numbers from the text and rejects the
/// narrative if any of them fails to match a fact price (within a small tolerance). Small bare
/// integers — wave labels (1–5) and low percentages — are allowed, since they are legitimate prose,
/// not fabricated prices. Pure and static so the guard is exhaustively unit-testable.
/// </summary>
public static partial class PositionFactGuard
{
    /// <summary>Relative tolerance when matching a mentioned price to a fact price (0.5%).</summary>
    private const decimal Tolerance = 0.005m;

    /// <summary>Numbers at or above this magnitude are treated as prices even without a decimal point.</summary>
    private const decimal PriceMagnitude = 1000m;

    /// <summary>
    /// True when every price-like number in <paramref name="narrative"/> matches one of the brief's
    /// fact prices. A narrative with no price-like numbers passes trivially.
    /// </summary>
    public static bool Passes(string narrative, PositionBrief brief)
    {
        ArgumentNullException.ThrowIfNull(narrative);
        ArgumentNullException.ThrowIfNull(brief);

        var facts = FactPrices(brief);
        foreach (Match match in NumberPattern().Matches(narrative))
        {
            var raw = match.Value.Replace(",", "", StringComparison.Ordinal);
            if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            // A number written as a percentage (immediately followed by '%') is legitimate prose —
            // a Fibonacci ratio or a move size — not a fabricated price.
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

    /// <summary>The set of legitimate fact prices a narrative may cite for <paramref name="brief"/>.</summary>
    public static IReadOnlyList<decimal> FactPrices(PositionBrief brief)
    {
        ArgumentNullException.ThrowIfNull(brief);

        var prices = new List<decimal>();
        if (brief.CurrentPrice is { } price)
        {
            prices.Add(price);
        }

        if (brief.Invalidation is { } inv)
        {
            prices.Add(inv.Price);
        }

        AddZone(prices, brief.EntryZone);
        foreach (var target in brief.TargetZones)
        {
            AddZone(prices, target);
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
