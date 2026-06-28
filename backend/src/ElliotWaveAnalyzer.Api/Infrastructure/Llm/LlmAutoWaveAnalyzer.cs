using System.Text.Json;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Llm;

/// <summary>
/// Ranks machine-generated wave candidates via an <see cref="IChatClient"/>. Mirrors
/// <see cref="LlmWaveAnalyzer"/> in structure (provider-agnostic, JSON in / JSON out, token
/// usage extraction) but for the full-auto flow: it sends candidate ids + geometry and parses
/// back a ranking that references those ids only.
/// </summary>
internal sealed class LlmAutoWaveAnalyzer(
    IChatClient chatClient,
    IOptions<LlmProviderOptions> options,
    ILogger<LlmAutoWaveAnalyzer> logger) : IAutoWaveAnalyzer
{
    private const int MaxOutputTokens = 2048;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc/>
    public string ProviderName => options.Value.Active;

    /// <inheritdoc/>
    public async Task<AutoWaveAnalysis> RankAsync(
        string symbol,
        IReadOnlyList<MarketCandle> candles,
        IReadOnlyList<WaveCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        var prompt = AutoWaveAnalysisPromptBuilder.Build(symbol, candles, candidates);

        logger.LogInformation(
            "Ranking {Count} wave candidates for {Symbol} via {Provider} ({Model})",
            candidates.Count, symbol, ProviderName, options.Value.GetActiveEndpoint().Model);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System,
                "You are an expert Elliott Wave market analyst. Respond ONLY with valid JSON — no prose, no markdown."),
            new(ChatRole.User, prompt),
        };

        var chatOptions = new ChatOptions
        {
            ModelId = options.Value.GetActiveEndpoint().Model,
            MaxOutputTokens = MaxOutputTokens,
        };

        ChatResponse response;
        try
        {
            response = await chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "{Provider} chat request failed", ProviderName);
            throw new InvalidOperationException(
                $"{ProviderName} request failed: {ex.Message}. " +
                "Check the API key and model name in appsettings.json.", ex);
        }

        var text = response.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException(
                $"{ProviderName} returned an empty response. Check the API key and model name in appsettings.json.");
        }

        var usage = ToTokenUsage(response.Usage);
        var ranking = ParseRankingJson(text);
        return new AutoWaveAnalysis(ranking, usage);
    }

    private TokenUsage ToTokenUsage(UsageDetails? details)
    {
        var prompt = (int)(details?.InputTokenCount ?? 0);
        var completion = (int)(details?.OutputTokenCount ?? 0);
        var total = (int)(details?.TotalTokenCount ?? (prompt + completion));
        return new TokenUsage(ProviderName, prompt, completion, total);
    }

    private AutoWaveRanking ParseRankingJson(string text)
    {
        // Defensive fence stripping — some models wrap JSON in ```json … ``` despite instructions.
        var cleaned = text.Trim();
        if (cleaned.StartsWith("```", StringComparison.Ordinal))
        {
            var start = cleaned.IndexOf('\n');
            var end = cleaned.LastIndexOf("```", StringComparison.Ordinal);
            if (start >= 0 && end > start)
            {
                cleaned = cleaned[(start + 1)..end].Trim();
            }
        }

        AutoRankingDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<AutoRankingDto>(cleaned, JsonOptions)
                  ?? throw new InvalidOperationException("Ranking JSON deserialized to null.");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Could not parse {Provider} ranking JSON: {Json}", ProviderName, cleaned);
            throw new InvalidOperationException(
                $"{ProviderName} returned a response that is not valid JSON. Raw: {cleaned}", ex);
        }

        var rankings = (dto.Rankings ?? [])
            .Select(r => new RankedCandidate(
                CandidateId: r.CandidateId,
                Confidence: r.Confidence ?? "low",
                Rationale: r.Rationale ?? string.Empty,
                Outlook: r.Outlook ?? string.Empty))
            .ToList();

        return new AutoWaveRanking(dto.BestCandidateId, dto.MarketSummary ?? string.Empty, rankings);
    }

    private sealed class AutoRankingDto
    {
        public int BestCandidateId { get; init; }
        public string? MarketSummary { get; init; }
        public RankedCandidateDto[]? Rankings { get; init; }
    }

    private sealed class RankedCandidateDto
    {
        public int CandidateId { get; init; }
        public string? Confidence { get; init; }
        public string? Rationale { get; init; }
        public string? Outlook { get; init; }
    }
}
