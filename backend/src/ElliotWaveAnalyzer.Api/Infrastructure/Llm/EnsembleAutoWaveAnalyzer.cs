using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Llm;

/// <summary>
/// Multi-provider ("ensemble") ranker: asks every configured provider to rank the candidates,
/// then aggregates their answers into a single consensus ranking. This is the "multiple agents"
/// approach — each model is an independent analyst and the disagreement is surfaced, not hidden.
///
/// Only providers with a non-empty API key participate (resolving a keyed client with an empty
/// key would throw). A provider that fails at call time is skipped; the analysis still returns
/// as long as at least one provider succeeds.
///
/// Chat clients are obtained through <see cref="IChatClientResolver"/> (not a raw
/// <see cref="IServiceProvider"/>), so this class depends on an abstraction rather than
/// service-locating its collaborators.
/// </summary>
internal sealed class EnsembleAutoWaveAnalyzer(
    IChatClientResolver chatClientResolver,
    IOptions<LlmProviderOptions> options,
    INarrativeLanguageProvider languageProvider,
    ILogger<EnsembleAutoWaveAnalyzer> logger) : IAutoWaveAnalyzer
{
    /// <summary>The keyed-DI name, endpoint options, and display name for each provider.</summary>
    private static readonly (string Key, Func<LlmProviderOptions, LlmEndpointOptions> Endpoint, string Display)[] Providers =
    [
        ("gemini", o => o.Gemini, "Gemini"),
        ("claude", o => o.Claude, "Claude"),
        ("openai", o => o.OpenAI, "OpenAI"),
    ];

    /// <inheritdoc/>
    public string ProviderName => "Ensemble";

    /// <inheritdoc/>
    public async Task<AutoWaveAnalysis> RankAsync(
        string symbol,
        IReadOnlyList<MarketCandle> candles,
        IReadOnlyList<WaveCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        var enabled = Providers
            .Where(p => !string.IsNullOrWhiteSpace(p.Endpoint(opts).ApiKey))
            .ToList();

        if (enabled.Count == 0)
        {
            throw new InvalidOperationException(
                "Ensemble mode is enabled but no provider has an API key configured.");
        }

        logger.LogInformation(
            "Ensemble ranking {Count} candidates for {Symbol} across: {Providers}",
            candidates.Count, symbol, string.Join(", ", enabled.Select(p => p.Display)));

        // Resolved once — the same caller's preference applies to every provider in the ensemble.
        var language = await languageProvider.GetCurrentAsync(cancellationToken);

        // Query all providers concurrently; tolerate individual failures.
        var tasks = enabled.Select(async p =>
        {
            try
            {
                var client = chatClientResolver.Resolve(p.Key);
                var result = await AutoWaveRankRunner.RunAsync(
                    client, p.Endpoint(opts).Model, p.Display, symbol, candles, candidates, logger, cancellationToken,
                    language: language);
                return (p.Display, Result: result, Ok: true);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Ensemble provider {Provider} failed; skipping", p.Display);
                return (p.Display, Result: default(AutoWaveAnalysis)!, Ok: false);
            }
        });

        var results = (await Task.WhenAll(tasks)).Where(r => r.Ok).ToList();
        if (results.Count == 0)
        {
            throw new InvalidOperationException("All ensemble providers failed to return a ranking.");
        }

        return Aggregate(results.Select(r => (r.Display, r.Result)).ToList());
    }

    /// <summary>Merges per-provider rankings into a single consensus <see cref="AutoWaveAnalysis"/>.</summary>
    private static AutoWaveAnalysis Aggregate(IReadOnlyList<(string Provider, AutoWaveAnalysis Analysis)> results)
    {
        // Token usage is the sum across every provider that ran.
        var totalPrompt = results.Sum(r => r.Analysis.Usage.PromptTokens);
        var totalCompletion = results.Sum(r => r.Analysis.Usage.CompletionTokens);
        var totalTokens = results.Sum(r => r.Analysis.Usage.TotalTokens);
        var providerLabel = $"Ensemble ({string.Join("+", results.Select(r => r.Provider))})";
        var usage = new TokenUsage(providerLabel, totalPrompt, totalCompletion, totalTokens);

        // Consensus best candidate: majority vote across providers' picks.
        var votes = results
            .GroupBy(r => r.Analysis.Ranking.BestCandidateId)
            .Select(g => (Id: g.Key, Count: g.Count()))
            .OrderByDescending(v => v.Count)
            .ThenBy(v => v.Id)
            .ToList();
        var bestId = votes[0].Id;
        var agreeing = votes[0].Count;

        // Union of all candidate ids any provider ranked, ordered with the consensus best first.
        var candidateIds = results
            .SelectMany(r => r.Analysis.Ranking.Rankings.Select(rc => rc.CandidateId))
            .Distinct()
            .OrderBy(id => id == bestId ? 0 : 1)
            .ThenBy(id => id)
            .ToList();

        var merged = candidateIds.Select(id =>
        {
            var perProvider = results
                .Select(r => (r.Provider, Ranked: r.Analysis.Ranking.Rankings.FirstOrDefault(rc => rc.CandidateId == id)))
                .Where(x => x.Ranked is not null)
                .ToList();

            var confidence = ModeOrDefault(perProvider.Select(x => x.Ranked!.Confidence), "low");
            var rationale = string.Join(" ",
                perProvider.Where(x => !string.IsNullOrWhiteSpace(x.Ranked!.Rationale))
                           .Select(x => $"[{x.Provider}] {x.Ranked!.Rationale}"));
            var outlook = string.Join(" ",
                perProvider.Where(x => !string.IsNullOrWhiteSpace(x.Ranked!.Outlook))
                           .Select(x => $"[{x.Provider}] {x.Ranked!.Outlook}"));

            return new RankedCandidate(id, confidence, rationale, outlook);
        }).ToList();

        var consensus = $"Consensus: {agreeing}/{results.Count} models favour count #{bestId}. ";
        var perProviderSummaries = string.Join(" ",
            results.Where(r => !string.IsNullOrWhiteSpace(r.Analysis.Ranking.MarketSummary))
                   .Select(r => $"[{r.Provider}] {r.Analysis.Ranking.MarketSummary}"));

        var ranking = new AutoWaveRanking(bestId, consensus + perProviderSummaries, merged);
        return new AutoWaveAnalysis(ranking, usage);
    }

    /// <summary>Returns the most common value, or <paramref name="fallback"/> when the sequence is empty.</summary>
    private static string ModeOrDefault(IEnumerable<string> values, string fallback)
    {
        var groups = values
            .GroupBy(v => v)
            .Select(g => (Value: g.Key, Count: g.Count()))
            .OrderByDescending(g => g.Count)
            .ToList();
        return groups.Count > 0 ? groups[0].Value : fallback;
    }
}
