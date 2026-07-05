namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>A computed Fibonacci ratio between waves (e.g. wave-2 retracement of wave 1).</summary>
public sealed record FibRatio(string Name, decimal Ratio);
