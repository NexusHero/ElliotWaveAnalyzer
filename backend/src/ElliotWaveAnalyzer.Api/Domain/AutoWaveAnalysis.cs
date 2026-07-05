namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>Pairs the pure <see cref="AutoWaveRanking"/> with the token cost of the LLM call.</summary>
public sealed record AutoWaveAnalysis(AutoWaveRanking Ranking, TokenUsage Usage);
