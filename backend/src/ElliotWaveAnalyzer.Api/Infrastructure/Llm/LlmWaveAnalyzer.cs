using System.Text.Json;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure.Gemini;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Llm;

/// <summary>
/// The single Elliott Wave validator. It is provider-agnostic: it talks to an
/// <see cref="IChatClient"/> (from Microsoft.Extensions.AI) and does not know or care
/// whether the backing service is Gemini, Claude, or OpenAI — that choice is made once
/// at startup in Program.cs based on <see cref="LlmProviderOptions.Active"/>.
///
/// WHY this replaced the three hand-rolled HttpClient providers:
/// per-provider HTTP plumbing, request/response DTOs, and token-usage extraction are
/// exactly what <see cref="IChatClient"/> standardizes. We keep only what is genuinely
/// ours: the prompt (<see cref="GeminiPromptBuilder"/>) and the JSON result shape.
/// </summary>
public sealed class LlmWaveAnalyzer(
    IChatClient chatClient,
    IOptions<LlmProviderOptions> options,
    ILogger<LlmWaveAnalyzer> logger) : ILlmWaveAnalyzer
{
    private const int MaxOutputTokens = 2048;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc/>
    public string ProviderName => options.Value.Active;

    /// <inheritdoc/>
    public async Task<WaveValidationResult> ValidateAsync(
        string symbol,
        IReadOnlyList<MarketCandle> candles,
        IReadOnlyList<WaveAnnotation> annotations,
        CancellationToken cancellationToken = default)
    {
        var prompt = GeminiPromptBuilder.Build(symbol, candles, annotations);

        logger.LogInformation(
            "Sending wave validation to {Provider} ({Model}) for {Symbol} with {Count} annotations",
            ProviderName, options.Value.GetActiveEndpoint().Model, symbol, annotations.Count);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System,
                "You are an expert Elliott Wave analyst. Respond ONLY with valid JSON — no prose, no markdown."),
            new(ChatRole.User, prompt),
        };

        // ModelId is set per-request so every provider is handled uniformly
        // (Anthropic's IChatClient takes the model here rather than at construction).
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
        logger.LogDebug("{Provider} token usage: {Usage}", ProviderName, usage);

        return ParseValidationJson(text, usage);
    }

    private TokenUsage ToTokenUsage(UsageDetails? details)
    {
        var prompt = (int)(details?.InputTokenCount ?? 0);
        var completion = (int)(details?.OutputTokenCount ?? 0);
        var total = (int)(details?.TotalTokenCount ?? (prompt + completion));

        return new TokenUsage(ProviderName, prompt, completion, total);
    }

    private WaveValidationResult ParseValidationJson(string text, TokenUsage usage)
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

        WaveValidationDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<WaveValidationDto>(cleaned, JsonOptions)
                  ?? throw new InvalidOperationException("Validation JSON deserialized to null.");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Could not parse {Provider} validation JSON: {Json}", ProviderName, cleaned);
            throw new InvalidOperationException(
                $"{ProviderName} returned a response that is not valid JSON. Raw: {cleaned}", ex);
        }

        return new WaveValidationResult(
            IsValid: dto.IsValid,
            Violations: dto.Violations ?? [],
            Warnings: dto.Warnings ?? [],
            Analysis: dto.Analysis ?? string.Empty,
            Confidence: dto.Confidence ?? "low",
            TokenUsage: usage);
    }

    private sealed class WaveValidationDto
    {
        public bool IsValid { get; init; }
        public string[]? Violations { get; init; }
        public string[]? Warnings { get; init; }
        public string? Analysis { get; init; }
        public string? Confidence { get; init; }
    }
}
