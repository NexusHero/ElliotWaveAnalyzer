using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Elliott's theoretical core made measurable: compares price at a count's "conviction" wave (3 for
/// an impulse, A for a correction) against its "extension" wave (5, or C) to the nearest social-mood
/// reading at each pivot. When price extends further in the same direction but mood does not confirm
/// — the classic "new high on fading conviction" — that pivot is flagged as a divergence. Pure and
/// deterministic: the same pivots and sentiment series always yield the same flags, and this never
/// touches the count's own geometry (AC5) — it only reads <see cref="WaveAnnotation"/> pivots, never
/// mutates them.
/// </summary>
public static class MoodDivergenceDetector
{
    /// <summary>(earlier conviction wave, later extension wave) pairs to compare, by label.</summary>
    private static readonly (string Earlier, string Later)[] ConvictionPairs =
    [
        ("3", "5"),
        ("A", "C"),
    ];

    /// <summary>
    /// Flags a divergence for each conviction/extension pair present in <paramref name="pivots"/>
    /// whose nearest mood readings fail to confirm the pivots' price move. A pair missing either
    /// label, or with no nearby sentiment coverage, contributes nothing.
    /// </summary>
    public static IReadOnlyList<MoodDivergence> Detect(
        IReadOnlyList<WaveAnnotation> pivots,
        IReadOnlyList<SentimentPoint> sentiment)
    {
        ArgumentNullException.ThrowIfNull(pivots);
        ArgumentNullException.ThrowIfNull(sentiment);

        if (pivots.Count == 0 || sentiment.Count == 0)
        {
            return [];
        }

        var byLabel = new Dictionary<string, WaveAnnotation>(StringComparer.OrdinalIgnoreCase);
        foreach (var pivot in pivots)
        {
            byLabel[pivot.Label] = pivot;
        }

        var divergences = new List<MoodDivergence>();
        foreach (var (earlierLabel, laterLabel) in ConvictionPairs)
        {
            if (!byLabel.TryGetValue(earlierLabel, out var earlier) ||
                !byLabel.TryGetValue(laterLabel, out var later) ||
                later.Price == earlier.Price)
            {
                continue;
            }

            var earlierMood = NearestScore(sentiment, earlier.Date);
            var laterMood = NearestScore(sentiment, later.Date);
            if (earlierMood is not { } em || laterMood is not { } lm)
            {
                continue;
            }

            var bullishExtension = later.Price > earlier.Price;
            var moodConfirmed = bullishExtension ? lm > em : lm < em;
            if (moodConfirmed)
            {
                continue;
            }

            divergences.Add(new MoodDivergence(
                later.Label,
                later.Date,
                bullishExtension ? MoodDivergenceKind.Bearish : MoodDivergenceKind.Bullish,
                em,
                lm));
        }

        return divergences;
    }

    /// <summary>The mood score of whichever reading's date is closest to <paramref name="date"/>.</summary>
    private static double? NearestScore(IReadOnlyList<SentimentPoint> sentiment, DateTime date)
    {
        SentimentPoint? nearest = null;
        var bestDiff = TimeSpan.MaxValue;
        foreach (var point in sentiment)
        {
            var diff = (point.Date - date).Duration();
            if (diff < bestDiff)
            {
                bestDiff = diff;
                nearest = point;
            }
        }

        return nearest?.Score;
    }
}
