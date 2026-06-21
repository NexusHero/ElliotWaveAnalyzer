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
/// Calls the Google Gemini REST API for Elliott Wave validation.
/// Uses <c>responseMimeType: application/json</c> so the response is always
/// parseable without stripping markdown fences.
/// Token usage is read from <c>usageMetadata</c> in the Gemini response.
/// </summary>
public sealed class GeminiLlmProvider(
    HttpClient httpClient,
    IOptions<LlmProviderOptions> options,
    ILogger<GeminiLlmProvider> logger) : ILlmWaveAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <inheritdoc/>
    public string ProviderName => "Gemini";

    /// <inheritdoc/>
    public async Task<WaveValidationResult> ValidateAsync(
        string symbol,
        IReadOnlyList<MarketCandle> candles,
        IReadOnlyList<WaveAnnotation> annotations,
        CancellationToken cancellationToken = default)
    {
        var cfg = options.Value.Gemini;
        var prompt = GeminiPromptBuilder.Build(symbol, candles, annotations);

        logger.LogInformation(
            "Sending wave validation to Gemini ({Model}) for {Symbol} with {Count} annotations",
            cfg.Model, symbol, annotations.Count);

        var url = $"v1beta/models/{Uri.EscapeDataString(cfg.Model)}:generateContent?key={cfg.ApiKey}";

        var body = new
        {
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = prompt } } }
            },
            generationConfig = new { responseMimeType = "application/json" }
        };

        var response = await httpClient.PostAsJsonAsync(url, body, cancellationToken);
        response.EnsureSuccessStatusCode();

        var apiResponse = await response.Content
            .ReadFromJsonAsync<GeminiApiResponse>(JsonOptions, cancellationToken);

        var text = apiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException(
                "Gemini returned an empty response. Check the API key and model name in appsettings.json.");
        }

        var usage = new TokenUsage(
            Provider: ProviderName,
            PromptTokens: apiResponse?.UsageMetadata?.PromptTokenCount ?? 0,
            CompletionTokens: apiResponse?.UsageMetadata?.CandidatesTokenCount ?? 0,
            TotalTokens: apiResponse?.UsageMetadata?.TotalTokenCount ?? 0);

        logger.LogDebug("Gemini token usage: {Usage}", usage);

        return ParseValidationJson(text, usage);
    }

    private WaveValidationResult ParseValidationJson(string json, TokenUsage usage)
    {
        GeminiValidationDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<GeminiValidationDto>(json, JsonOptions)
                  ?? throw new InvalidOperationException("Gemini validation JSON deserialized to null.");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Could not parse Gemini validation JSON: {Json}", json);
            throw new InvalidOperationException(
                $"Gemini returned a response that is not valid JSON. Raw: {json}", ex);
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

    private sealed class GeminiApiResponse
    {
        public GeminiCandidate[]? Candidates { get; init; }
        public GeminiUsageMetadata? UsageMetadata { get; init; }
    }

    private sealed class GeminiCandidate
    {
        public GeminiContent? Content { get; init; }
    }

    private sealed class GeminiContent
    {
        public GeminiPart[]? Parts { get; init; }
    }

    private sealed class GeminiPart
    {
        public string? Text { get; init; }
    }

    private sealed class GeminiUsageMetadata
    {
        public int PromptTokenCount { get; init; }
        public int CandidatesTokenCount { get; init; }
        public int TotalTokenCount { get; init; }
    }

    private sealed class GeminiValidationDto
    {
        public bool IsValid { get; init; }
        public string[]? Violations { get; init; }
        public string[]? Warnings { get; init; }
        public string? Analysis { get; init; }
        public string? Confidence { get; init; }
    }
}
