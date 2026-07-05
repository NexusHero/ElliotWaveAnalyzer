namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The LLM's pure ranking of the candidates (ids + prose only — no geometry). Paired with
/// <see cref="TokenUsage"/> in <see cref="AutoWaveAnalysis"/>.
/// </summary>
/// <param name="BestCandidateId">Id of the most likely candidate.</param>
/// <param name="MarketSummary">One-paragraph read of the overall market structure.</param>
/// <param name="Rankings">Per-candidate assessment, most likely first.</param>
public sealed record AutoWaveRanking(
    int BestCandidateId,
    string MarketSummary,
    IReadOnlyList<RankedCandidate> Rankings);
