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
/// Calls the OpenAI Chat Completions API for Elliott Wave validation.
/// Uses <c>response_format: { type: "json_object" }</c> for reliable JSON output.
/// Token usage is read from the <c>usage</c> field in the response.
///
/// Docs: https://platform.openai.com/docs/api-reference/chat
/// </summary>
public sealed class OpenAiLlmProvider(
    HttpClient httpClient,
    IOptions<LlmProviderOptions> options,
    ILogger<OpenAiLlmProvider> logger) : ILlmWaveAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <inheritdoc/>
    public string ProviderName => "OpenAI";

    /// <inheritdoc/>
    public async Task<WaveValidationResult> ValidateAsync(
        string symbol,
        IReadOnlyList<MarketCandle> candles,
        IReadOnlyList<WaveAnnotation> annotations,
        CancellationToken cancellationToken = default)
    {
        var cfg = options.Value.OpenAI;
        var prompt = GeminiPromptBuilder.Build(symbol, candles, annotations);

        logger.LogInformation(
            "Sending wave validation to OpenAI ({Model}) for {Symbol} with {Count} annotations",
            cfg.Model, symbol, annotations.Count);

        var body = new
        {
            model = cfg.Model,
            // JSON mode — OpenAI guarantees a JSON object when this is set
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = "You are an expert Elliott Wave analyst. Respond ONLY with valid JSON matching the schema provided by the user."
                },
                new { role = "user", content = prompt }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {cfg.ApiKey}");
        request.Content = JsonContent.Create(body, options: JsonOptions);

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var apiResponse = await response.Content
            .ReadFromJsonAsync<OpenAiChatResponse>(JsonOptions, cancellationToken);

        var text = apiResponse?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException(
                "OpenAI returned an empty response. Check the API key and model name in appsettings.json.");
        }

        var usage = new TokenUsage(
            Provider: ProviderName,
            PromptTokens: apiResponse?.Usage?.PromptTokens ?? 0,
            CompletionTokens: apiResponse?.Usage?.CompletionTokens ?? 0,
            TotalTokens: apiResponse?.Usage?.TotalTokens ?? 0);

        logger.LogDebug("OpenAI token usage: {Usage}", usage);

        return ParseValidationJson(text, usage);
    }

    private WaveValidationResult ParseValidationJson(string json, TokenUsage usage)
    {
        OpenAiValidationDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<OpenAiValidationDto>(json, JsonOptions)
                  ?? throw new InvalidOperationException("OpenAI validation JSON deserialized to null.");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Could not parse OpenAI validation JSON: {Json}", json);
            throw new InvalidOperationException(
                $"OpenAI returned a response that is not valid JSON. Raw: {json}", ex);
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

    private sealed class OpenAiChatResponse
    {
        public OpenAiChoice[]? Choices { get; init; }
        public OpenAiUsage? Usage { get; init; }
    }

    private sealed class OpenAiChoice
    {
        public OpenAiMessage? Message { get; init; }
    }

    private sealed class OpenAiMessage
    {
        public string? Content { get; init; }
    }

    private sealed class OpenAiUsage
    {
        public int PromptTokens { get; init; }
        public int CompletionTokens { get; init; }
        public int TotalTokens { get; init; }
    }

    private sealed class OpenAiValidationDto
    {
        public bool IsValid { get; init; }
        public string[]? Violations { get; init; }
        public string[]? Warnings { get; init; }
        public string? Analysis { get; init; }
        public string? Confidence { get; init; }
    }
}
