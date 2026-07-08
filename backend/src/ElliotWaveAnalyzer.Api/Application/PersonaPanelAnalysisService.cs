using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Orchestrates the full persona panel (#184) — the panel's equivalent of
/// <see cref="AutoWaveAnalysisService"/>: detect pivots, generate the deterministic candidate
/// set, hand it to the panel to rank, then aggregate and merge the panel's answer back onto the
/// candidates' own geometry. The candidate set itself never depends on which personas ran or how
/// many (AC1) — it is generated once, before any LLM call.
/// </summary>
public sealed class PersonaPanelAnalysisService(
    IEnumerable<IMarketDataProvider> marketDataProviders,
    IPersonaAnalystPanel panel,
    ITokenTracker tokenTracker,
    IIndicatorCalculator indicatorCalculator,
    ILogger<PersonaPanelAnalysisService> logger) : IPersonaPanelAnalysisService
{
    private readonly IReadOnlyList<IMarketDataProvider> _marketDataProviders = [.. marketDataProviders];

    public async Task<PersonaPanelResponse> AnalyzeAsync(
        Guid userId,
        string symbol,
        int lookbackDays,
        decimal thresholdPercent,
        CancellationToken cancellationToken = default)
    {
        if (tokenTracker.IsBudgetExceeded())
        {
            var report = tokenTracker.GetReport();
            throw new InvalidOperationException(
                $"Session token budget of {report.Budget:N0} tokens has been exceeded " +
                $"(used: {report.SessionTotalTokens:N0}). Restart the server to reset, " +
                "or increase LlmProvider:TokenBudget in appsettings.json.");
        }

        var marketProvider = _marketDataProviders.FirstOrDefault(p => p.Supports(symbol))
            ?? throw new ArgumentException($"No market data provider supports symbol '{symbol}'.", nameof(symbol));

        var candles = await marketProvider.GetCandlesAsync(symbol, lookbackDays, cancellationToken);

        var pivots = SwingPivotDetector.Detect(candles, thresholdPercent);
        var (candidates, searchTruncated) = WaveCandidateGenerator.GenerateParsed(
            pivots, candles: candles, indicatorCalculator: indicatorCalculator, cancellationToken: cancellationToken);

        logger.LogInformation(
            "Persona panel for {Symbol}: {Pivots} pivots → {Candidates} parsed candidates (truncated: {Truncated})",
            symbol, pivots.Count, candidates.Count, searchTruncated);

        if (candidates.Count == 0)
        {
            return new PersonaPanelResponse(
                Rankings: [],
                Weights: [],
                ConsensusScore: 0.0,
                MarketSummary: "No rule-valid Elliott Wave structure was detected for the " +
                    "selected period and sensitivity. Try a longer lookback or a different threshold.",
                Usage: new TokenUsage("Persona panel", 0, 0, 0))
            { SearchTruncated = searchTruncated, PersonasAttempted = 0 };
        }

        var result = await panel.RankAsync(userId, symbol, candles, candidates, cancellationToken);
        tokenTracker.Record(result.Usage);
        logger.LogInformation(
            "Persona panel token usage — provider: {Provider}, total: {T}, personas attempted: {N}",
            result.Usage.Provider, result.Usage.TotalTokens, result.PersonasAttempted);

        var validIds = candidates.Select(c => c.Id).ToHashSet();
        var consensus = PersonaPanelAggregator.Aggregate(result.Rankings, result.Weights, validIds);

        var byId = candidates.ToDictionary(c => c.Id);
        var endorsingByCandidate = result.Rankings
            .Where(r => byId.ContainsKey(r.Ranking.BestCandidateId))
            .GroupBy(r => r.Ranking.BestCandidateId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(r => r.Persona).ToList());

        var merged = consensus.Rankings
            .Where(rc => byId.ContainsKey(rc.CandidateId))
            .Select(rc =>
            {
                var candidate = byId[rc.CandidateId];
                var endorsing = endorsingByCandidate.TryGetValue(rc.CandidateId, out var personas) ? personas : [];
                return new PersonaRankedCount(
                    candidate.Structure, candidate.Origin, candidate.Waves, candidate.RuleReport, candidate.Levels,
                    rc.Confidence, rc.Rationale, rc.Outlook, IsBest: rc.CandidateId == consensus.BestCandidateId,
                    EndorsingPersonas: endorsing)
                { Tree = candidate.Tree, Score = candidate.Score };
            })
            .OrderByDescending(r => r.IsBest)
            .ToList();

        var marketSummary = string.Join(" ",
            result.Rankings.Where(r => !string.IsNullOrWhiteSpace(r.Ranking.MarketSummary))
                .Select(r => $"[{r.Persona}] {r.Ranking.MarketSummary}"));

        return new PersonaPanelResponse(merged, consensus.Weights, consensus.ConsensusScore, marketSummary, result.Usage)
        {
            SearchTruncated = searchTruncated,
            PersonasAttempted = result.PersonasAttempted,
        };
    }
}
