namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A detected mood-vs-wave-position divergence (mirrors a <see cref="RuleResult"/> — a deterministic
/// finding, not an LLM opinion): at <see cref="PivotLabel"/>'s pivot, price extended further than the
/// count's prior conviction wave while social mood did not confirm. Produced only by
/// <see cref="Application.MoodDivergenceDetector"/>.
/// </summary>
/// <param name="PivotLabel">The wave label where the divergence shows (e.g. "5" or "C").</param>
/// <param name="Date">The pivot's date.</param>
/// <param name="Kind">Which way the divergence points.</param>
/// <param name="EarlierMood">Mood score at the earlier conviction wave (e.g. wave 3 / wave A).</param>
/// <param name="LaterMood">Mood score at <see cref="PivotLabel"/>'s pivot.</param>
public sealed record MoodDivergence(
    string PivotLabel,
    DateTime Date,
    MoodDivergenceKind Kind,
    double EarlierMood,
    double LaterMood);
