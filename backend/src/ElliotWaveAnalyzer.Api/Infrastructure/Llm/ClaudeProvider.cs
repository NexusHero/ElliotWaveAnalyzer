using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure.Gemini;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Llm;

/// <summary>
/// Calls the Anthropic Claude API (Messages endpoint) for Elliott Wave validation.
/// Uses a system prompt to enforce JSON-only output since Claude does not
/// support <c>response_format: json_object</c> the same way OpenAI does.
/// Token usage is read from the <c>usage</c> field in the response.
///
/// Docs: https://docs.anthropic.com/en/api/messages
/// </summary>
public sealed class ClaudeProvider(
    HttpClient httpClient,
    IOptions<LlmProviderOptions> options,
    ILogger<ClaudeProvider> logger) : ILlmWaveAnalyzer
{
    private const string AnthropicVersion = "2023-06-01";
    private const int MaxTokens = 2048;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <inheritdoc/>
    public string ProviderName => "Claude";

    /// <inheritdoc/>
    public async Task<WaveValidationResult> ValidateAsync(
        string symbol,
        IReadOnlyList<MarketCandle> candles,
        IReadOnlyList<WaveAnnotation> annotations,
        CancellationToken cancellationToken = default)
    {
        var cfg = options.Value.Claude;
        var prompt = GeminiPromptBuilder.Build(symbol, candles, annotations);

        logger.LogInformation(
            "Sending wave validation to Claude ({Model}) for {Symbol} with {Count} annotations",
            cfg.Model, symbol, annotations.Count);

        var body = new
        {
            model = cfg.Model,
            max_tokens = MaxTokens,
            // Reinforce JSON-only via system prompt — Claude follows instructions reliably
            system = "You are an expert Elliott Wave analyst. Respond ONLY with valid JSON — no prose, no markdown.",
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/messages");
        request.Headers.Add("x-api-key", cfg.ApiKey);
        request.Headers.Add("anthropic-version", AnthropicVersion);
        request.Content = JsonContent.Create(body, options: JsonOptions);

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var apiResponse = await response.Content
            .ReadFromJsonAsync<ClaudeApiResponse>(JsonOptions, cancellationToken);

        var text = apiResponse?.Content?.FirstOrDefault(c => c.Type == "text")?.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException(
                "Claude returned an empty response. Check the API key and model name in appsettings.json.");
        }

        var usage = new TokenUsage(
            Provider: ProviderName,
            PromptTokens: apiResponse?.Usage?.InputTokens ?? 0,
            CompletionTokens: apiResponse?.Usage?.OutputTokens ?? 0,
            TotalTokens: (apiResponse?.Usage?.InputTokens ?? 0) + (apiResponse?.Usage?.OutputTokens ?? 0));

        logger.LogDebug("Claude token usage: {Usage}", usage);

        return ParseValidationJson(text, usage);
    }

    private WaveValidationResult ParseValidationJson(string json, TokenUsage usage)
    {
        // Claude may occasionally wrap output in ```json ... ``` even when instructed not to.
        // Strip fences defensively.
        var cleaned = json.Trim();
        if (cleaned.StartsWith("```", StringComparison.Ordinal))
        {
            var start = cleaned.IndexOf('\n');
            var end = cleaned.LastIndexOf("```", StringComparison.Ordinal);
            if (start >= 0 && end > start)
                cleaned = cleaned[(start + 1)..end].Trim();
        }

        ClaudeValidationDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<ClaudeValidationDto>(cleaned, JsonOptions)
                  ?? throw new InvalidOperationException("Claude validation JSON deserialized to null.");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Could not parse Claude validation JSON: {Json}", cleaned);
            throw new InvalidOperationException(
                $"Claude returned a response that is not valid JSON. Raw: {cleaned}", ex);
        }

        return new WaveValidationResult(
            IsValid: dto.IsValid,
            Violations: dto.Violations ?? [],
            Warnings: dto.Warnings ?? [],
            Analysis: dto.Analysis ?? string.Empty,
            Confidence: dto.Confidence ?? "low",
            TokenUsage: usage);
    }

    // ─── Response DTOs ────────────────────────────────────────────────────────

    private sealed class ClaudeApiResponse
    {
        public ClaudeContentBlock[]? Content { get; init; }
        public ClaudeUsage? Usage { get; init; }
    }

    private sealed class ClaudeContentBlock
    {
        public string? Type { get; init; }
        public string? Text { get; init; }
    }

    private sealed class ClaudeUsage
    {
        public int InputTokens { get; init; }
        public int OutputTokens { get; init; }
    }

    private sealed class ClaudeValidationDto
    {
        public bool IsValid { get; init; }
        public string[]? Violations { get; init; }
        public string[]? Warnings { get; init; }
        public string? Analysis { get; init; }
        public string? Confidence { get; init; }
    }
}
