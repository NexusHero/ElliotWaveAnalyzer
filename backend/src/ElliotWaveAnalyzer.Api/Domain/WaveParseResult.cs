namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Result of a grammar parse. <see cref="SearchTruncated"/> is true when the evaluation
/// budget stopped the search early — the returned trees are still valid and deterministic,
/// but coverage was bounded (never silently: callers should surface this).
/// </summary>
public sealed record WaveParseResult(IReadOnlyList<ScoredWaveTree> Trees, bool SearchTruncated);
