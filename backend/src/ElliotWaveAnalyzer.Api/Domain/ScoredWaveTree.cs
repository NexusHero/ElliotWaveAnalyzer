namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>A complete parsed count: the nested tree plus its overall score.</summary>
public sealed record ScoredWaveTree(WaveNode Root, decimal Score);
