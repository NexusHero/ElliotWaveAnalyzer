using System.Globalization;
using System.Text;
using System.Text.Json;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Llm;

/// <summary>
/// Narrates a position from its deterministic facts via the configured <see cref="IChatClient"/>. The
/// facts are injected into the prompt and the model is told to cite only them; its output is then run
/// through <see cref="PositionFactGuard"/>, so a narrative that invents a price is rejected rather than
/// shown. Degrades gracefully: with no chat client registered (no key), or on a transport/parse
/// failure, it returns an explicit reason and no narrative — the deterministic brief still stands.
/// </summary>
internal sealed class LlmPositionNarrator(
    IEnumerable<IChatClient> chatClients,
    IOptions<LlmProviderOptions> options,
    INarrativeLanguageProvider languageProvider,
    ILogger<LlmPositionNarrator> logger) : IPositionNarrator
{
    private const int MaxOutputTokens = 512;
    private readonly IChatClient? _chatClient = chatClients.FirstOrDefault();

    /// <inheritdoc/>
    public async Task<PositionNarration> NarrateAsync(
        PositionBrief brief, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(brief);

        if (_chatClient is null)
        {
            return PositionNarration.Unavailable("No LLM provider is configured — add an API key to enable narratives.");
        }

        var systemPrompt =
            "You are an Elliott Wave analyst writing a one-paragraph position note. Use ONLY the numbers "
            + "in the fact sheet — never state a price that is not listed. Respond ONLY with JSON: "
            + "{\"narrative\": \"...\"}.";
        var language = await languageProvider.GetCurrentAsync(cancellationToken);
        if (NarrativeLanguageDirective.For(language) is { } languageDirective)
        {
            systemPrompt += " " + languageDirective;
        }

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, BuildFactSheet(brief)),
        };

        var chatOptions = new ChatOptions
        {
            ModelId = options.Value.GetActiveEndpoint().Model,
            MaxOutputTokens = MaxOutputTokens,
            ResponseFormat = ChatResponseFormat.Json,
        };

        string? narrative;
        try
        {
            var response = await _chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
            narrative = ParseNarrative(response.Text);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Position narrative generation failed for {Symbol}", brief.Symbol);
            return PositionNarration.Unavailable("The narrative service is temporarily unavailable.");
        }

        if (string.IsNullOrWhiteSpace(narrative))
        {
            return PositionNarration.Unavailable("The narrative service returned no usable text.");
        }

        if (!PositionFactGuard.Passes(narrative, brief))
        {
            logger.LogWarning("Rejected narrative for {Symbol}: cited a price not in the fact sheet", brief.Symbol);
            return PositionNarration.Unavailable("The generated narrative failed the fact check and was withheld.");
        }

        return PositionNarration.Of(narrative.Trim());
    }

    private static string BuildFactSheet(PositionBrief brief)
    {
        var sb = new StringBuilder();
        sb.Append("Instrument: ").Append(brief.Name).Append(" (").Append(brief.Symbol).AppendLine(")");
        sb.Append("Direction: ").AppendLine(brief.Bullish ? "bullish" : "bearish");
        sb.Append("Count chain: ").AppendLine(brief.ChainSummary);
        AppendPrice(sb, "Current price", brief.CurrentPrice);
        AppendPrice(sb, "Invalidation", brief.Invalidation?.Price);
        if (brief.EntryZone is { } entry)
        {
            sb.Append("Entry zone: ").Append(Fmt(entry.Low)).Append(" to ").AppendLine(Fmt(entry.High));
        }

        foreach (var target in brief.TargetZones)
        {
            sb.Append("Target zone: ").Append(Fmt(target.Low)).Append(" to ").AppendLine(Fmt(target.High));
        }

        return sb.ToString();
    }

    private static void AppendPrice(StringBuilder sb, string label, decimal? value)
    {
        if (value is { } v)
        {
            sb.Append(label).Append(": ").AppendLine(Fmt(v));
        }
    }

    private static string Fmt(decimal value)
        => value.ToString(value >= 1000m ? "N0" : "0.####", CultureInfo.InvariantCulture);

    private static string? ParseNarrative(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(text[start..(end + 1)]);
            return doc.RootElement.TryGetProperty("narrative", out var value) ? value.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
