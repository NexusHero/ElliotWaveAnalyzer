namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// One day's normalized social-mood reading for a symbol (mirrors an indicator sample — see
/// <see cref="RsiResult"/>). <see cref="Score"/> is clamped to [-1, 1]: -1 maximally bearish mood,
/// +1 maximally bullish, 0 neutral. Produced only by <see cref="Application.SentimentIndexBuilder"/>
/// from a provider's raw reading — never invented.
/// </summary>
/// <param name="Date">UTC date the reading covers.</param>
/// <param name="Score">Normalized mood in [-1, 1].</param>
public sealed record SentimentPoint(DateTime Date, double Score);
