namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Which way a mood-vs-price divergence points (mirrors classic momentum-divergence reading, applied
/// to social mood instead of an oscillator). <see cref="Bearish"/>: price extended further in the
/// trend's direction but mood did not confirm — a warning the move's conviction is fading.
/// <see cref="Bullish"/>: price extended further against the trend but mood did not confirm as
/// negative — a sign selling conviction is exhausted.
/// </summary>
public enum MoodDivergenceKind
{
    Bearish,
    Bullish,
}
