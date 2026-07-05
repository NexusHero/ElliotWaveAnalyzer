namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// One Fibonacci level that feeds a <see cref="ConfluenceZone"/>. <see cref="Weight"/> reflects the
/// degree it comes from (higher degree = more significant). <see cref="Basis"/> is the analyst-style
/// label, e.g. "61.8% retracement of (1)→(2), log scale".
/// </summary>
public sealed record ContributingLevel(decimal Price, decimal Weight, string Basis);
