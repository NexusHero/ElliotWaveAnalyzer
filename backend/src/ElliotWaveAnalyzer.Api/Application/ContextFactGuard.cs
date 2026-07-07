using System.Globalization;
using System.Text.RegularExpressions;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// The anti-hallucination guard for the context overlay (#188, sibling of
/// <see cref="PositionFactGuard"/>/<see cref="AnalogFactGuard"/>/<see cref="SentimentFactGuard"/>/
/// <see cref="ThesisFactGuard"/>): a narrative may only cite the correlation coefficients and percent
/// moves the report actually computed — a fabricated correlation or intermarket move number is
/// rejected. Only decimal-point numbers are checked: correlations and percent moves are always
/// reported with a decimal, so this alone catches a fabrication, while leaving catalyst dates
/// (e.g. "2026-03-15"), wave labels and turn-date day-counts — all bare integers — as legitimate,
/// unchecked prose, exactly like the sibling guards treat wave numbers. Pure and static so the guard
/// is exhaustively unit-testable.
/// </summary>
public static partial class ContextFactGuard
{
    /// <summary>Relative tolerance when matching a mentioned number to a fact (0.5%).</summary>
    private const decimal Tolerance = 0.005m;

    /// <summary>
    /// True when every decimal-point number in <paramref name="narrative"/> matches one of the
    /// report's computed numbers. A narrative with no decimal-point numbers passes trivially.
    /// </summary>
    public static bool Passes(string narrative, ContextReport report)
    {
        ArgumentNullException.ThrowIfNull(narrative);
        ArgumentNullException.ThrowIfNull(report);

        var facts = FactNumbers(report);
        foreach (Match match in NumberPattern().Matches(narrative))
        {
            var raw = match.Value.Replace(",", "", StringComparison.Ordinal);
            if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            // A number written as a percentage (immediately followed by '%') is legitimate prose —
            // not a fabricated correlation or move — same exemption as the sibling guards.
            var after = match.Index + match.Length;
            if (after < narrative.Length && narrative[after] == '%')
            {
                continue;
            }

            // Only decimal-point numbers are fact-checked (see class remarks) — bare integers
            // (dates, wave labels, day-counts) are never in the report's own number set to compare
            // against, so treating them as fact-like would misfire on a legitimately-cited date.
            if (raw.Contains('.', StringComparison.Ordinal) && !MatchesAFact(value, facts))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The set of legitimate fact numbers a narrative may cite for <paramref name="report"/>.</summary>
    public static IReadOnlyList<decimal> FactNumbers(ContextReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var numbers = new List<decimal>();
        foreach (var signal in report.IntermarketSignals)
        {
            numbers.Add((decimal)signal.Correlation);
            numbers.Add(signal.PercentChange);
        }

        foreach (var flag in report.CatalystFlags)
        {
            numbers.Add(flag.DaysFromTurn);
        }

        return numbers;
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

    [GeneratedRegex(@"-?[0-9][0-9,]*(?:\.[0-9]+)?")]
    private static partial Regex NumberPattern();
}
