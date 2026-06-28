using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Llm;

/// <summary>
/// Ranks machine-generated wave candidates via the single active <see cref="IChatClient"/>.
/// The chat call and JSON parsing live in <see cref="AutoWaveRankRunner"/>, shared with
/// <see cref="EnsembleAutoWaveAnalyzer"/>; this class just supplies the active provider's
/// client and model.
/// </summary>
internal sealed class LlmAutoWaveAnalyzer(
    IChatClient chatClient,
    IOptions<LlmProviderOptions> options,
    ILogger<LlmAutoWaveAnalyzer> logger) : IAutoWaveAnalyzer
{
    /// <inheritdoc/>
    public string ProviderName => options.Value.Active;

    /// <inheritdoc/>
    public Task<AutoWaveAnalysis> RankAsync(
        string symbol,
        IReadOnlyList<MarketCandle> candles,
        IReadOnlyList<WaveCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Ranking {Count} wave candidates for {Symbol} via {Provider} ({Model})",
            candidates.Count, symbol, ProviderName, options.Value.GetActiveEndpoint().Model);

        return AutoWaveRankRunner.RunAsync(
            chatClient,
            options.Value.GetActiveEndpoint().Model,
            ProviderName,
            symbol,
            candles,
            candidates,
            logger,
            cancellationToken);
    }
}
