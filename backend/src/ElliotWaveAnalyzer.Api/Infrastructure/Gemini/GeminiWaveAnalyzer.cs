using System.Text.Json;
using System.Text.Json.Serialization;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Google.GenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Gemini;

/// <summary>
/// Sends Elliott Wave validation requests to Gemini via the official Google.GenAI SDK.
///
/// ISOLATION: Google.GenAI types are referenced only in this file.
/// All other code depends on <see cref="IGeminiWaveAnalyzer"/> only.
///
/// JSON parsing: Gemini is instructed to respond with JSON only (ResponseMimeType).
/// We deserialize the response into <see cref="GeminiResponseDto"/> and map to
/// the domain <see cref="WaveValidationResult"/>. If Gemini returns malformed JSON,
/// we throw <see cref="InvalidOperationException"/> so the endpoint returns 502.
/// </summary>
public sealed class GeminiWaveAnalyzer(
    IOptions<GeminiOptions> options,
    ILogger<GeminiWaveAnalyzer> logger) : IGeminiWaveAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <inheritdoc/>
    public async Task<WaveValidationResult> ValidateAsync(
        string symbol,
        IReadOnlyList<MarketCandle> candles,
        IReadOnlyList<WaveAnnotation> annotations,
        CancellationToken cancellationToken = default)
    {
        var prompt = GeminiPromptBuilder.Build(symbol, candles, annotations);

        logger.LogInformation(
            "Sending wave validation request to Gemini ({Model}) for {Symbol} with {Count} annotations",
            options.Value.Model, symbol, annotations.Count);

        var rawJson = await CallGeminiAsync(prompt, cancellationToken);

        logger.LogDebug("Gemini raw response: {Response}", rawJson);

        return ParseResponse(rawJson);
    }

    // ─── Gemini API call ──────────────────────────────────────────────────────

    private async Task<string> CallGeminiAsync(string prompt, CancellationToken ct)
    {
        // Google.GenAI: https://github.com/googleapis/dotnet-genai
        // Client reads GOOGLE_API_KEY env var by default, or accepts ClientConfig.
        var client = new Client(new ClientConfig
        {
            ApiKey = options.Value.ApiKey,
        });

        var config = new Types.GenerateContentConfig
        {
            // Force JSON output — Gemini will not add markdown fences or prose.
            ResponseMimeType = "application/json",
        };

        var response = await client.Models.GenerateContentAsync(
            model: options.Value.Model,
            contents: prompt,
            config: config);

        // Safety: check we got a candidate with content
        var text = response?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            logger.LogError("Gemini returned empty content for prompt (first 200 chars): {Prompt}",
                prompt[..Math.Min(200, prompt.Length)]);
            throw new InvalidOperationException(
                "Gemini returned an empty response. Check the API key and model name.");
        }

        return text;
    }

    // ─── JSON parsing ─────────────────────────────────────────────────────────

    private WaveValidationResult ParseResponse(string json)
    {
        GeminiResponseDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<GeminiResponseDto>(json, JsonOptions)
                  ?? throw new InvalidOperationException("Gemini response deserialized to null.");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Gemini returned non-JSON response: {Json}", json);
            throw new InvalidOperationException(
                $"Gemini returned a response that could not be parsed as JSON. Raw: {json}", ex);
        }

        return new WaveValidationResult(
            IsValid: dto.IsValid,
            Violations: dto.Violations ?? [],
            Warnings: dto.Warnings ?? [],
            Analysis: dto.Analysis ?? string.Empty,
            Confidence: dto.Confidence ?? "low");
    }

    // ─── Private DTO (maps Gemini JSON → domain model) ────────────────────────

    /// <summary>
    /// Internal DTO matching the JSON schema we instruct Gemini to produce.
    /// Not exposed outside this class — callers receive <see cref="WaveValidationResult"/>.
    /// </summary>
    private sealed class GeminiResponseDto
    {
        public bool IsValid { get; init; }
        public string[]? Violations { get; init; }
        public string[]? Warnings { get; init; }
        public string? Analysis { get; init; }
        public string? Confidence { get; init; }
    }
}
