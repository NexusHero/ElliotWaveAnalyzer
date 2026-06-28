using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Orchestrates the full-auto wave analysis (the "magic button"):
/// 1. Checks the token budget (abort before any work if exceeded).
/// 2. Fetches candle history for the symbol.
/// 3. Detects swing pivots deterministically (<see cref="SwingPivotDetector"/>).
/// 4. Generates rule-valid candidate counts (<see cref="WaveCandidateGenerator"/>).
/// 5. If candidates exist, has the LLM rank + explain them; otherwise returns an empty
///    ranking WITHOUT calling the LLM (no candidates = nothing to spend tokens on).
///
/// The deterministic geometry from steps 3–4 is what the response carries; the LLM only
/// supplies confidence, rationale and outlook keyed by candidate id.
/// </summary>
public sealed class AutoWaveAnalysisService(
    IEnumerable<IMarketDataProvider> marketDataProviders,
    IAutoWaveAnalyzer llm,
    ITokenTracker tokenTracker,
    ILogger<AutoWaveAnalysisService> logger) : IAutoWaveAnalysisService
{
    private readonly IReadOnlyList<IMarketDataProvider> _marketDataProviders = [.. marketDataProviders];

    /// <inheritdoc/>
    public async Task<AutoWaveAnalysisResponse> AnalyzeAsync(
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
            ?? throw new ArgumentException(
                $"No market data provider supports symbol '{symbol}'.", nameof(symbol));

        var candles = await marketProvider.GetCandlesAsync(symbol, lookbackDays, cancellationToken);

        var pivots = SwingPivotDetector.Detect(candles, thresholdPercent);
        var candidates = WaveCandidateGenerator.Generate(pivots);

        logger.LogInformation(
            "Auto analysis for {Symbol}: {Pivots} pivots → {Candidates} rule-valid candidates",
            symbol, pivots.Count, candidates.Count);

        if (candidates.Count == 0)
        {
            // Nothing to rank — don't spend tokens. Return an empty, well-formed response.
            return new AutoWaveAnalysisResponse(
                Rankings: [],
                MarketSummary: "No rule-valid Elliott Wave structure was detected for the " +
                    "selected period and sensitivity. Try a longer lookback or a different threshold.",
                Usage: new TokenUsage(llm.ProviderName, 0, 0, 0));
        }

        var analysis = await llm.RankAsync(symbol, candles, candidates, cancellationToken);
        tokenTracker.Record(analysis.Usage);
        logger.LogInformation(
            "Token usage — provider: {Provider}, prompt: {P}, completion: {C}, total: {T}",
            analysis.Usage.Provider, analysis.Usage.PromptTokens,
            analysis.Usage.CompletionTokens, analysis.Usage.TotalTokens);

        var rankings = MergeRankings(candidates, analysis.Ranking);
        return new AutoWaveAnalysisResponse(rankings, analysis.Ranking.MarketSummary, analysis.Usage);
    }

    /// <summary>
    /// Joins the deterministic candidate geometry with the LLM's per-candidate prose, ordered
    /// as the LLM ranked them. Candidates the LLM omitted are appended (so geometry is never
    /// silently dropped); if the LLM's "best" id is unknown, the first ranked candidate wins.
    /// </summary>
    private static IReadOnlyList<RankedWaveCount> MergeRankings(
        IReadOnlyList<WaveCandidate> candidates, AutoWaveRanking ranking)
    {
        var byId = candidates.ToDictionary(c => c.Id);
        var result = new List<RankedWaveCount>();
        var seen = new HashSet<int>();

        var bestId = byId.ContainsKey(ranking.BestCandidateId)
            ? ranking.BestCandidateId
            : ranking.Rankings.FirstOrDefault(r => byId.ContainsKey(r.CandidateId))?.CandidateId
              ?? candidates[0].Id;

        foreach (var ranked in ranking.Rankings)
        {
            if (!byId.TryGetValue(ranked.CandidateId, out var candidate) || !seen.Add(ranked.CandidateId))
            {
                continue; // unknown or duplicate id from the model
            }

            result.Add(ToRanked(candidate, ranked.Confidence, ranked.Rationale, ranked.Outlook, candidate.Id == bestId));
        }

        // Append any candidate the model didn't mention, so nothing is lost.
        foreach (var candidate in candidates.Where(c => !seen.Contains(c.Id)))
        {
            result.Add(ToRanked(candidate, "low", string.Empty, string.Empty, candidate.Id == bestId));
        }

        // Best first.
        return [.. result.OrderByDescending(r => r.IsBest)];
    }

    private static RankedWaveCount ToRanked(
        WaveCandidate c, string confidence, string rationale, string outlook, bool isBest)
        => new(c.Structure, c.Origin, c.Waves, c.RuleReport, confidence, rationale, outlook, isBest);
}
