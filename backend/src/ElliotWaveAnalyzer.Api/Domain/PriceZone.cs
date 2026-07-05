namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>A price band (e.g. the expected Fibonacci retracement zone for the unfolding wave).</summary>
/// <param name="Low">Lower bound.</param>
/// <param name="High">Upper bound.</param>
/// <param name="Label">Short human label, e.g. "Wave 4 support (23.6–38.2% of Wave 3)".</param>
/// <param name="Basis">Derivation, e.g. "Fibonacci retracement of Wave 3".</param>
public sealed record PriceZone(decimal Low, decimal High, string Label, string Basis);
