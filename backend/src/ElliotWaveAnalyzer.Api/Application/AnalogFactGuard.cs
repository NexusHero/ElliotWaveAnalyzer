using System.Globalization;
using System.Text.RegularExpressions;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// The anti-hallucination guard for the historical-analog narrative: the LLM may only cite numbers
/// that the deterministic <see cref="AnalogReport"/> actually computed. It rejects the narrative if
/// <list type="bullet">
///   <item>any <b>percentage</b> in the text does not match the measured hit-rate (or its complement,
///   for "…% failed") within a percentage point — this is the classic hallucinated-rate case; or</item>
///   <item>any <b>statistic-like</b> number (a decimal, or an integer ≥ 10 such as a sample count,
///   a day count, or a year) does not match one of the report's facts.</item>
/// </list>
/// Small bare integers (&lt; 10, no percent sign) are allowed as legitimate prose — wave labels, "the
/// two that failed" — mirroring <see cref="PositionFactGuard"/>. Pure and static, so it is exhaustively
/// unit-testable and the same seam guards every analog summary.
/// </summary>
public static partial class AnalogFactGuard
{
    /// <summary>Tolerance (percentage points) when matching a mentioned rate to the measured hit-rate.</summary>
    private const double RateTolerance = 1.0;

    /// <summary>Tolerance when matching a mentioned integer statistic (day count, sample, year).</summary>
    private const double IntegerTolerance = 0.5;

    /// <summary>Bare integers below this magnitude are treated as prose, not fabricated statistics.</summary>
    private const double StatisticMagnitude = 10.0;

    /// <summary>
    /// True when every statistic-like number in <paramref name="narrative"/> matches a fact in
    /// <paramref name="report"/>. A narrative with no such numbers passes trivially.
    /// </summary>
    public static bool Passes(string narrative, AnalogReport report)
    {
        ArgumentNullException.ThrowIfNull(narrative);
        ArgumentNullException.ThrowIfNull(report);

        var rates = AllowedRates(report);
        var integers = AllowedIntegers(report);

        foreach (Match match in NumberPattern().Matches(narrative))
        {
            var raw = match.Value.Replace(",", "", StringComparison.Ordinal);
            if (!double.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            var after = match.Index + match.Length;
            var isPercent = after < narrative.Length && narrative[after] == '%';
            if (isPercent)
            {
                if (!MatchesWithin(value, rates, RateTolerance)) return false;
                continue;
            }

            var isStatistic = raw.Contains('.', StringComparison.Ordinal) || value >= StatisticMagnitude;
            if (isStatistic && !MatchesWithin(value, integers, IntegerTolerance))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The percentages the narrative may cite: the hit-rate and its complement (the miss-rate).</summary>
    private static IReadOnlyList<double> AllowedRates(AnalogReport report)
    {
        if (report.Stats.HitRate is not { } hit) return [];
        var hitPct = hit * 100.0;
        return [hitPct, 100.0 - hitPct];
    }

    /// <summary>The integer statistics the narrative may cite: counts, median days, per-analog days and years.</summary>
    private static IReadOnlyList<double> AllowedIntegers(AnalogReport report)
    {
        var facts = new List<double>
        {
            report.Stats.SampleCount,
            report.Stats.TargetReached,
            report.Stats.Invalidated,
        };
        if (report.Stats.MedianResolutionDays is { } median) facts.Add(median);

        foreach (var analog in report.Analogs)
        {
            if (analog.Setup.ResolutionDays is { } days) facts.Add(days);
            facts.Add(analog.Setup.FormedAt.Year);
            if (analog.Setup.ConcludedAt is { } concluded) facts.Add(concluded.Year);
        }

        return facts;
    }

    private static bool MatchesWithin(double value, IReadOnlyList<double> facts, double tolerance) =>
        facts.Any(fact => Math.Abs(fact - value) <= tolerance);

    [GeneratedRegex(@"[0-9][0-9,]*(?:\.[0-9]+)?")]
    private static partial Regex NumberPattern();
}
