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
    // too small a cap truncates the JSON mid-string.
    private const int MaxOutputTokens = 16384;

    // Initial attempt + one corrective retry: a model that truncated or emitted malformed JSON
    // tends to repeat itself unless told what went wrong (same discipline as the vision extractor).
    private const int MaxAttempts = 2;

    private const string RetryNudge =
        "Your previous response was cut off or was not valid JSON. Respond again with ONLY the "
        + "complete JSON object described above — no prose, no markdown — and keep marketSummary "
        + "to at most three sentences and every rationale/outlook to at most two short sentences.";

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

        var promptTokens = 0;
        var completionTokens = 0;
        for (var attempt = 1; ; attempt++)
        {
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

            promptTokens += (int)(response.Usage?.InputTokenCount ?? 0);
            completionTokens += (int)(response.Usage?.OutputTokenCount ?? 0);

            var text = response.Text;
            if (!string.IsNullOrWhiteSpace(text) && TryParseRankingJson(text, provider, logger, out var ranking))
            {
                var usage = new TokenUsage(
                    provider, promptTokens, completionTokens, promptTokens + completionTokens);
                return new AutoWaveAnalysis(ranking, usage);
            }

            if (attempt >= MaxAttempts)
            {
                throw new InvalidOperationException(
                    $"{provider} did not return a complete, valid ranking JSON after {MaxAttempts} attempts. " +
                    "Try again, or lower the sensitivity so fewer candidates are ranked.");
            }

            // Tell the model what went wrong instead of resending the identical prompt — a
            // truncated response usually means the rationales ran long; ask for brevity.
            logger.LogWarning(
                "{Provider} ranking response was empty, truncated or malformed (attempt {Attempt}/{Max}) — retrying with corrective feedback",
                provider, attempt, MaxAttempts);
            messages.Add(new(ChatRole.User, RetryNudge));
        }
    }

    private static bool TryParseRankingJson(
        string text, string provider, ILogger logger, out AutoWaveRanking ranking)
    {
        // Models sometimes wrap JSON in ```fences``` or add prose — extract the object robustly.
        var cleaned = LlmJson.ExtractObject(text);

        AutoRankingDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<AutoRankingDto>(cleaned, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Could not parse {Provider} ranking JSON: {Json}", provider, cleaned);
            ranking = null!;
            return false;
        }

        if (dto is null)
        {
            ranking = null!;
            return false;
        }

        var rankings = (dto.Rankings ?? [])
            .Select(r => new RankedCandidate(
                CandidateId: r.CandidateId,
                Confidence: r.Confidence ?? "low",
                Rationale: r.Rationale ?? string.Empty,
                Outlook: r.Outlook ?? string.Empty))
            .ToList();

        ranking = new AutoWaveRanking(dto.BestCandidateId, dto.MarketSummary ?? string.Empty, rankings);
        return true;
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
