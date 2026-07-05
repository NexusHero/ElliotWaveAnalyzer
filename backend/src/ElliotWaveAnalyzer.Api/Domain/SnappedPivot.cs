namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A claimed pivot that snapped to a real candle extreme within tolerance: its label and the actual
/// candle date and extreme price it locked onto (the claimed price is kept for transparency).
/// </summary>
/// <param name="Label">The Elliott label from the claim.</param>
/// <param name="Date">The real candle date it snapped to.</param>
/// <param name="Price">The real candle extreme (high or low) it snapped to.</param>
/// <param name="ClaimedPrice">The price the model originally read.</param>
public sealed record SnappedPivot(string Label, DateTime Date, decimal Price, decimal ClaimedPrice);
