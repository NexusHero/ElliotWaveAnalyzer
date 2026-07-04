using System.Text.Json;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using Microsoft.Extensions.AI;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Llm;

/// <summary>
/// Runs a single candidate-ranking call against one <see cref="IChatClient"/> and parses the
/// JSON result. Shared by the single-provider <see cref="LlmAutoWaveAnalyzer"/> and the
/// multi-provider <see cref="EnsembleAutoWaveAnalyzer"/> so the prompt, chat options and JSON
/// contract live in exactly one place.
/// </summary>
internal static class AutoWaveRankRunner
{
    // Generous cap: ranking several candidates (summary + per-candidate rationale & outlook)
    // is verbose, and Gemini 2.5 models spend "thinking" tokens against this same budget —
    // too small a cap truncates the JSON mid-string. 8192 leaves ample room.
    private const int MaxOutputTokens = 8192;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Sends the candidates to <paramref name="chatClient"/> and returns the parsed ranking with
    /// token usage. Throws <see cref="InvalidOperationException"/> on transport or JSON failure.
    /// </summary>
    public static async Task<AutoWaveAnalysis> RunAsync(
        IChatClient chatClient,
        string model,
        string provider,
        string symbol,
        IReadOnlyList<MarketCandle> candles,
        IReadOnlyList<WaveCandidate> candidates,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var prompt = AutoWaveAnalysisPromptBuilder.Build(symbol, candles, candidates);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System,
                "You are an expert Elliott Wave market analyst. Respond ONLY with valid JSON — no prose, no markdown."),
            new(ChatRole.User, prompt),
        };

        var chatOptions = new ChatOptions
        {
            ModelId = model,
            MaxOutputTokens = MaxOutputTokens,
            // Native JSON mode where the provider supports it (OpenAI/Gemini honor it,
            // others ignore it); LlmJson.ExtractObject stays as the fallback for providers
            // that still wrap the object in fences or prose.
            ResponseFormat = ChatResponseFormat.Json,
        };

        ChatResponse response;
        try
        {
            response = await chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "{Provider} chat request failed", provider);
            throw new InvalidOperationException(
                $"{provider} request failed: {ex.Message}. " +
                "Check the API key and model name in appsettings.json.", ex);
        }

        var text = response.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException(
                $"{provider} returned an empty response. Check the API key and model name in appsettings.json.");
        }

        var usage = ToTokenUsage(provider, response.Usage);
        var ranking = ParseRankingJson(text, provider, logger);
        return new AutoWaveAnalysis(ranking, usage);
    }

    private static TokenUsage ToTokenUsage(string provider, UsageDetails? details)
    {
        var prompt = (int)(details?.InputTokenCount ?? 0);
        var completion = (int)(details?.OutputTokenCount ?? 0);
        var total = (int)(details?.TotalTokenCount ?? (prompt + completion));
        return new TokenUsage(provider, prompt, completion, total);
    }

    private static AutoWaveRanking ParseRankingJson(string text, string provider, ILogger logger)
    {
        // Models sometimes wrap JSON in ```fences``` or add prose — extract the object robustly.
        var cleaned = LlmJson.ExtractObject(text);

        AutoRankingDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<AutoRankingDto>(cleaned, JsonOptions)
                  ?? throw new InvalidOperationException("Ranking JSON deserialized to null.");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Could not parse {Provider} ranking JSON: {Json}", provider, cleaned);
            throw new InvalidOperationException(
                $"{provider} returned a response that is not valid JSON. Raw: {cleaned}", ex);
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
