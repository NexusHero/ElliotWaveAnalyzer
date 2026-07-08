using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Llm;

/// <summary>
/// Runs every catalog persona (#184) sequentially over the same chat-client seam
/// <see cref="UserAwareChatClient"/> already provides — so the user's own key vs. the operator's
/// shared quota (#174) is honoured exactly as it is for every other LLM feature, with zero new
/// quota logic here.
///
/// AC5's cost-bounded degradation is reactive, not pre-emptive: personas run in a fixed order
/// (catalog order) and stop the moment one fails on quota exhaustion (<see cref="LlmQuotaExceededException"/>)
/// or any other transient error, keeping whatever succeeded so far — mirroring
/// <see cref="EnsembleAutoWaveAnalyzer"/>'s "tolerate individual failures" shape. Only when the
/// very first persona fails does the exception propagate (the same outcome a single-persona
/// ranking would already have — not a new failure mode).
/// </summary>
internal sealed class PersonaAnalystPanel(
    IChatClient chatClient,
    IOptions<LlmProviderOptions> options,
    IPersonaCalibrationProvider calibrationProvider,
    ILogger<PersonaAnalystPanel> logger) : IPersonaAnalystPanel
{
    public async Task<PersonaPanelRankResult> RankAsync(
        Guid userId,
        string symbol,
        IReadOnlyList<MarketCandle> candles,
        IReadOnlyList<WaveCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        var personaKeys = PersonaCatalog.Personas.Select(p => p.Key).ToList();
        var history = await calibrationProvider.GetHistoryAsync(userId, personaKeys, cancellationToken);
        var weights = PersonaWeightCalculator.Calculate(history);

        var opts = options.Value;
        var provider = opts.Active;
        var model = opts.GetActiveEndpoint().Model;

        var rankings = new List<PersonaRanking>();
        var totalPromptTokens = 0;
        var totalCompletionTokens = 0;

        foreach (var persona in PersonaCatalog.Personas)
        {
            try
            {
                var analysis = await AutoWaveRankRunner.RunAsync(
                    chatClient, model, $"{provider} ({persona.DisplayName})", symbol, candles, candidates,
                    logger, cancellationToken, persona.Guidance);

                rankings.Add(new PersonaRanking(persona.Key, analysis.Ranking));
                totalPromptTokens += analysis.Usage.PromptTokens;
                totalCompletionTokens += analysis.Usage.CompletionTokens;
            }
            catch (LlmQuotaExceededException) when (rankings.Count > 0)
            {
                // Cost-bounded degradation (AC5): keep whichever personas already succeeded rather
                // than spending (or failing on) quota the user doesn't have for the rest.
                logger.LogInformation(
                    "Persona panel for {Symbol} degraded to {Count} persona(s) — quota exhausted after {Persona}",
                    symbol, rankings.Count, persona.Key);
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && rankings.Count > 0)
            {
                logger.LogWarning(ex, "Persona {Persona} failed for {Symbol}; continuing with the rest", persona.Key, symbol);
            }
        }

        // Unreachable with an empty ranking set: the first persona's own catch clauses are guarded
        // by `rankings.Count > 0`, so a first-persona failure propagates out of the loop (and this
        // method) unchanged — exactly the same failure a single-persona ranking would already
        // surface, not a new one (see the class doc comment).
        var usage = new TokenUsage(
            $"Persona panel ({provider})", totalPromptTokens, totalCompletionTokens, totalPromptTokens + totalCompletionTokens);
        return new PersonaPanelRankResult(rankings, weights, usage, rankings.Count);
    }
}
