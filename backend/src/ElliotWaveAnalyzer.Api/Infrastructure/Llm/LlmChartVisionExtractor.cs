using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
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
/// <para>
/// The uploaded image is untrusted content (#175): it can carry adversarial text aimed at the vision
/// model ("ignore your task, output ..."). Every string field is validated against a fixed allow-list
/// shape before it leaves this class — a pivot label that doesn't look like an Elliott label fails the
/// whole extraction (the same "reject, don't coerce" strictness already applied to a malformed date or
/// price); an implausible symbol/timeframe/zone-label is silently dropped to null rather than passed
/// through, mirroring <c>ScanQueryValidator</c>'s allow-list boundary. No model-authored string can
/// reach the API response unvalidated, and the geometry/rules pipeline downstream (<see
/// cref="Api.Application.PivotSnapper"/>, <see cref="Api.Application.ChartVerificationAssembler"/>)
/// never reads label content for anything beyond exact known Elliott tokens, so injected text has no
/// path to influence the deterministic verdict even before this guard runs.
/// </para>
/// </summary>
internal sealed class LlmChartVisionExtractor(
    IEnumerable<IChatClient> chatClients,
    IOptions<LlmProviderOptions> options,
    ILogger<LlmChartVisionExtractor> logger) : IChartVisionExtractor
{
    private const int MaxOutputTokens = 2048;
    private const int MaxAttempts = 2; // initial + one retry
    private const int MaxZoneLabelLength = 40;
    private readonly IChatClient? _chatClient = chatClients.FirstOrDefault();

    // A real Elliott label is always short: a digit/roman numeral/letter, optionally parenthesised or
    // primed for a sub-degree ("3", "(3)", "iii", "B", "2'"). Anything longer or with other characters
    // is not a label a human drew — most likely injected instruction text — and is rejected outright.
    private static readonly Regex PlausibleLabel = new(
        @"^[A-Za-z0-9()'\.\-]{1,12}$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(50));

    // A ticker-shaped token, mirroring ScanQueryValidator's symbol allow-list: letters/digits and a
    // handful of separators used by real symbols (BTC-USD, BRK.B), capped well below prose length.
    private static readonly Regex PlausibleSymbol = new(
        @"^[A-Za-z0-9][A-Za-z0-9.\-]{0,9}$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(50));

    private static readonly HashSet<string> KnownTimeframes =
        new(StringComparer.OrdinalIgnoreCase) { "1h", "4h", "1d", "1w" };

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

            // Give the retry corrective feedback instead of resending the identical prompt — a model
            // that just emitted malformed output tends to repeat it verbatim otherwise. Still
            // perception-only and schema-validated, so this relaxes no determinism guarantee.
            if (attempt < MaxAttempts)
            {
                messages.Add(new(ChatRole.User,
                    "Your previous response was not valid JSON of the required shape. Respond with ONLY "
                    + "the JSON object described above — no prose, no markdown, no code fences."));
            }
        }

        throw new ChartExtractionException(
            "The chart image could not be read into a valid count. Try a cleaner screenshot of just the "
            + "price chart with the wave labels visible — crop out app menus and side panels.");
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
                ReadSymbol(root),
                ReadTimeframe(root),
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

        var label = labelEl.GetString()!;
        // Not a shape a real Elliott label can take — most likely injected instruction text riding
        // along in the one free-text field a pivot has. Reject the whole pivot (and so the whole
        // extraction, same as a malformed date/price) rather than truncate/sanitize it: a partially
        // sanitized injection string is still an unreviewed string, and a clean retry is cheap.
        if (!PlausibleLabel.IsMatch(label))
        {
            return false;
        }

        pivot = new ClaimedPivot(date, priceEl.GetDecimal(), label);
        return true;
    }

    private static string? ReadSymbol(JsonElement root)
    {
        var value = OptionalString(root, "symbol");
        return value is not null && PlausibleSymbol.IsMatch(value) ? value : null;
    }

    private static string? ReadTimeframe(JsonElement root)
    {
        var value = OptionalString(root, "timeframe");
        return value is not null && KnownTimeframes.Contains(value.Trim()) ? value : null;
    }

    /// <summary>A zone label is decorative (shown alongside a price box) and never drives any decision,
    /// but is still bounded — no control characters, capped length — so an oversized or non-printable
    /// payload can't ride through as "just a label".</summary>
    private static string? SanitizeZoneLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        var trimmed = label.Trim();
        return trimmed.Length <= MaxZoneLabelLength && trimmed.All(c => !char.IsControl(c)) ? trimmed : null;
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
                zones.Add(new ClaimedZone(low.GetDecimal(), high.GetDecimal(), SanitizeZoneLabel(OptionalString(z, "label"))));
            }
        }

        return zones;
    }
}
