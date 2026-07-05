namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>A price box the vision model claims was drawn on the chart (a target/entry zone).</summary>
/// <param name="Low">Lower bound the model read.</param>
/// <param name="High">Upper bound the model read.</param>
/// <param name="Label">Optional label drawn on the box.</param>
public sealed record ClaimedZone(decimal Low, decimal High, string? Label);
