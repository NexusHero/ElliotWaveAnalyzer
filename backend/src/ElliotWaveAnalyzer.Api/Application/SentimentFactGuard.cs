using System.Globalization;
using System.Text.RegularExpressions;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// The anti-hallucination guard for the socionomics narrative: the LLM may only cite mood scores that
/// the deterministic <see cref="SentimentReport"/> actually computed — never a reading it invented.
/// Every decimal-looking number in the narrative (a mood score, always written with a decimal point,
/// e.g. "0.62") must match one of the report's series readings or divergence figures within a small
/// tolerance; bare integers (wave labels, "two divergences") are prose, not statistics, and are
/// ignored — mirrors <see cref="AnalogFactGuard"/>. Pure and static, so it is exhaustively unit-tested
/// and the same seam guards every mood summary.
/// </summary>
public static partial class SentimentFactGuard
{
    /// <summary>Tolerance when matching a cited mood score to a computed reading.</summary>
    private const double MoodTolerance = 0.02;

    /// <summary>
    /// True when every mood score cited in <paramref name="narrative"/> matches a fact in
    /// <paramref name="report"/>. A narrative with no such numbers passes trivially.
    /// </summary>
    public static bool Passes(string narrative, SentimentReport report)
    {
        ArgumentNullException.ThrowIfNull(narrative);
        ArgumentNullException.ThrowIfNull(report);

        var moods = AllowedMoods(report);

        foreach (Match match in NumberPattern().Matches(narrative))
        {
            var raw = match.Value;
            if (!raw.Contains('.', StringComparison.Ordinal))
            {
                // Bare integers are wave labels or counts in prose, not cited mood scores.
                continue;
            }

            if (!double.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            if (!MatchesWithin(value, moods, MoodTolerance))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The mood magnitudes the narrative may cite: every series reading and divergence figure.</summary>
    private static IReadOnlyList<double> AllowedMoods(SentimentReport report)
    {
        var facts = new List<double>();
        foreach (var point in report.Series)
        {
            facts.Add(Math.Abs(point.Score));
        }

        foreach (var divergence in report.Divergences)
        {
            facts.Add(Math.Abs(divergence.EarlierMood));
            facts.Add(Math.Abs(divergence.LaterMood));
        }

        return facts;
    }

    private static bool MatchesWithin(double value, IReadOnlyList<double> facts, double tolerance) =>
        facts.Any(fact => Math.Abs(fact - value) <= tolerance);

    [GeneratedRegex(@"[0-9][0-9,]*(?:\.[0-9]+)?")]
    private static partial Regex NumberPattern();
}
