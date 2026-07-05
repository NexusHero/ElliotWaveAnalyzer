using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Pure Fibonacci retracement/extension math in either linear or log price space, plus the
/// auto-scale rule. Log math is done in ln-space so equal percentage moves are equal distances —
/// the correct model for instruments spanning multiples of their base price. Callers pass the
/// scale explicitly (chosen via <see cref="AutoSelect"/>) so it is always reported, never implicit.
/// </summary>
public static class FibMath
{
    /// <summary>
    /// The price that retraces the leg <paramref name="from"/> → <paramref name="to"/> back toward
    /// <paramref name="from"/> by <paramref name="fraction"/> (0 = the end, 1 = the start).
    /// </summary>
    public static decimal Retrace(decimal from, decimal to, decimal fraction, FibScale scale)
    {
        if (scale == FibScale.Log && from > 0m && to > 0m)
        {
            var f = Math.Log((double)from);
            var t = Math.Log((double)to);
            return (decimal)Math.Exp(t - (double)fraction * (t - f));
        }

        return to - fraction * (to - from);
    }

    /// <summary>
    /// Projects a move of <paramref name="multiple"/> × the leg <paramref name="from"/> →
    /// <paramref name="to"/> from <paramref name="basePrice"/>, in the leg's direction.
    /// </summary>
    public static decimal Extend(decimal basePrice, decimal from, decimal to, decimal multiple, FibScale scale)
    {
        if (scale == FibScale.Log && basePrice > 0m && from > 0m && to > 0m)
        {
            var span = Math.Log((double)to) - Math.Log((double)from);
            return (decimal)Math.Exp(Math.Log((double)basePrice) + (double)multiple * span);
        }

        return basePrice + multiple * (to - from);
    }

    /// <summary>
    /// Chooses log when the series spans more than <paramref name="ratioThreshold"/>× its low
    /// (default 3×), otherwise linear. Needs strictly positive prices for log.
    /// </summary>
    public static FibScale AutoSelect(IReadOnlyList<decimal> prices, decimal ratioThreshold = 3m)
    {
        if (prices.Count < 2)
        {
            return FibScale.Linear;
        }

        var min = prices.Min();
        var max = prices.Max();
        return min > 0m && max / min > ratioThreshold ? FibScale.Log : FibScale.Linear;
    }
}
