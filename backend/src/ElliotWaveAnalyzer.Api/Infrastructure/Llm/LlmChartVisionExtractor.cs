using System.Globalization;
using System.Text.Json;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Llm;

/// <summary>
/// Extracts a claimed count from a chart image via a vision-capable <see cref="IChatClient"/>, in
/// strict JSON mode, with schema validation and exactly one retry — mirroring the JSON discipline of
/// the wave ranker. Perception only: it returns claims, never verdicts. Throws
/// <see cref="ChartExtractionException"/> when the output can't be parsed after the retry, and
/// <see cref="InvalidOperationException"/> when no vision model is configured.
/// </summary>
internal sealed class LlmChartVisionExtractor(
    IEnumerable<IChatClient> chatClients,
    IOptions<LlmProviderOptions> options,
    ILogger<LlmChartVisionExtractor> logger) : IChartVisionExtractor
{
    private const int MaxOutputTokens = 2048;
    private const int MaxAttempts = 2; // initial + one retry
    private readonly IChatClient? _chatClient = chatClients.FirstOrDefault();

    private const string SystemPrompt =
        "You read Elliott Wave annotations off a chart image. Respond ONLY with JSON of the exact shape: "
        + "{\"symbol\": string|null, \"timeframe\": string|null, \"pivots\": [{\"approxDate\": \"YYYY-MM-DD\", "
        + "\"approxPrice\": number, \"label\": string}], \"levels\": [number], \"zones\": [{\"low\": number, "
        + "\"high\": number, \"label\": string|null}]}. Read the labels and approximate coordinates exactly as "
        + "drawn; do not invent pivots. No prose, no markdown.";

    /// <inheritdoc/>
    public async Task<ChartExtraction> ExtractAsync(
        byte[] image, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (_chatClient is null)
        {
            throw new InvalidOperationException("No vision-capable LLM is configured — add an API key to verify images.");
        }

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User,
                [new TextContent("Extract the Elliott Wave count drawn on this chart."), new DataContent(image, contentType)]),
        };
        var chatOptions = new ChatOptions
        {
            ModelId = options.Value.GetActiveEndpoint().Model,
            MaxOutputTokens = MaxOutputTokens,
            ResponseFormat = ChatResponseFormat.Json,
        };

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            ChatResponse response;
            try
            {
                response = await _chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Vision extraction request failed");
                throw new ChartExtractionException($"The vision request failed: {ex.Message}");
            }

            if (TryParse(response.Text, out var extraction))
            {
                return extraction;
            }

            logger.LogWarning("Vision extraction produced invalid JSON (attempt {Attempt}/{Max})", attempt, MaxAttempts);
        }

        throw new ChartExtractionException(
            "The chart image could not be read into a valid count (the model's output failed schema validation).");
    }

    /// <summary>Parses and validates the strict schema; returns false on any missing/invalid field.</summary>
    private static bool TryParse(string? text, out ChartExtraction extraction)
    {
        extraction = null!;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(text[start..(end + 1)]);
            var root = doc.RootElement;
            if (!root.TryGetProperty("pivots", out var pivotsEl) || pivotsEl.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var pivots = new List<ClaimedPivot>();
            foreach (var p in pivotsEl.EnumerateArray())
            {
                if (!TryPivot(p, out var pivot))
                {
                    return false;
                }

                pivots.Add(pivot);
            }

            if (pivots.Count == 0)
            {
                return false;
            }

            extraction = new ChartExtraction(
                OptionalString(root, "symbol"),
                OptionalString(root, "timeframe"),
                pivots,
                ReadLevels(root),
                ReadZones(root));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryPivot(JsonElement p, out ClaimedPivot pivot)
    {
        pivot = null!;
        if (p.ValueKind != JsonValueKind.Object
            || !p.TryGetProperty("approxDate", out var dateEl)
            || !p.TryGetProperty("approxPrice", out var priceEl)
            || !p.TryGetProperty("label", out var labelEl)
            || priceEl.ValueKind != JsonValueKind.Number
            || labelEl.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        if (!DateTime.TryParse(
            dateEl.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var date))
        {
            return false;
        }

        pivot = new ClaimedPivot(date, priceEl.GetDecimal(), labelEl.GetString()!);
        return true;
    }

    private static string? OptionalString(JsonElement root, string name)
        => root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

    private static IReadOnlyList<decimal> ReadLevels(JsonElement root)
    {
        if (!root.TryGetProperty("levels", out var el) || el.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return [.. el.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetDecimal())];
    }

    private static IReadOnlyList<ClaimedZone> ReadZones(JsonElement root)
    {
        if (!root.TryGetProperty("zones", out var el) || el.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var zones = new List<ClaimedZone>();
        foreach (var z in el.EnumerateArray())
        {
            if (z.ValueKind == JsonValueKind.Object
                && z.TryGetProperty("low", out var low) && low.ValueKind == JsonValueKind.Number
                && z.TryGetProperty("high", out var high) && high.ValueKind == JsonValueKind.Number)
            {
                zones.Add(new ClaimedZone(low.GetDecimal(), high.GetDecimal(), OptionalString(z, "label")));
            }
        }

        return zones;
    }
}
