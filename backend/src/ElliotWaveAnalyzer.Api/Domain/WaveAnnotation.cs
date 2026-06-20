namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A single user-placed Elliott Wave label on the price chart.
/// Represents the turning point (high or low) where a wave starts or ends.
/// </summary>
/// <param name="Date">UTC date of the candle the user clicked.</param>
/// <param name="Price">Price at the annotated point (typically the candle Close or High/Low).</param>
/// <param name="Label">
/// Elliott Wave label. Valid values:
/// Impulse:  "1" "2" "3" "4" "5"
/// Corrective: "A" "B" "C"
/// Complex corrective: "W" "X" "Y"
/// </param>
public sealed record WaveAnnotation(DateTime Date, decimal Price, string Label)
{
    private static readonly HashSet<string> ValidLabels = new(StringComparer.OrdinalIgnoreCase)
        { "1", "2", "3", "4", "5", "A", "B", "C", "W", "X", "Y" };

    /// <summary>Returns true when <paramref name="label"/> is a recognized Elliott Wave label.</summary>
    public static bool IsValidLabel(string label) => ValidLabels.Contains(label);
}
