namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Pure, deterministic Pearson-correlation math over aligned return series — the "how correlated"
/// computation behind the intermarket context overlay (#188). Reproducible: the same two series always
/// produce the same coefficient. No LLM, no I/O.
/// </summary>
public static class CorrelationCalculator
{
    /// <summary>
    /// Pearson correlation coefficient of <paramref name="a"/> and <paramref name="b"/>, in [-1, 1].
    /// Returns 0.0 when there are fewer than two paired observations, or either series is constant
    /// (zero variance) — a degenerate case with no meaningful correlation, not an error.
    /// </summary>
    public static double Pearson(IReadOnlyList<double> a, IReadOnlyList<double> b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.Count != b.Count)
        {
            throw new ArgumentException("Series must be the same length as the first series.", nameof(b));
        }

        if (a.Count < 2)
        {
            return 0.0;
        }

        var meanA = a.Average();
        var meanB = b.Average();

        double covariance = 0, varianceA = 0, varianceB = 0;
        for (var i = 0; i < a.Count; i++)
        {
            var deltaA = a[i] - meanA;
            var deltaB = b[i] - meanB;
            covariance += deltaA * deltaB;
            varianceA += deltaA * deltaA;
            varianceB += deltaB * deltaB;
        }

        if (varianceA == 0 || varianceB == 0)
        {
            return 0.0;
        }

        return covariance / Math.Sqrt(varianceA * varianceB);
    }

    /// <summary>Day-over-day percent returns of a close-price series (length N-1 for N closes).</summary>
    public static IReadOnlyList<double> PercentReturns(IReadOnlyList<decimal> closes)
    {
        ArgumentNullException.ThrowIfNull(closes);

        var returns = new List<double>();
        for (var i = 1; i < closes.Count; i++)
        {
            var previous = closes[i - 1];
            returns.Add(previous == 0 ? 0.0 : (double)((closes[i] - previous) / previous));
        }

        return returns;
    }
}
