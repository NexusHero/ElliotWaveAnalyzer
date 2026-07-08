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
/// Summarises a deterministic <see cref="AnalogReport"/> via the configured <see cref="IChatClient"/>.
/// The model receives only the computed facts (the aggregate rates and a digest of the analogs) and is
/// told to cite only them; its output is then run through <see cref="AnalogFactGuard"/>, so a summary
/// that invents a rate, count or date is withheld rather than shown. Degrades gracefully: no chat
/// client (no key), too few analogs, or a transport/parse failure returns the report unchanged with an
/// explicit reason — the empirical read always stands on its own.
/// </summary>
internal sealed class LlmAnalogNarrator(
    IEnumerable<IChatClient> chatClients,
    IOptions<LlmProviderOptions> options,
    INarrativeLanguageProvider languageProvider,
    ILogger<LlmAnalogNarrator> logger) : IAnalogNarrator
{
    private const int MaxOutputTokens = 400;
    private readonly IChatClient? _chatClient = chatClients.FirstOrDefault();

    /// <inheritdoc/>
    public async Task<AnalogReport> NarrateAsync(AnalogReport report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (_chatClient is null)
        {
            return report with
            {
                NarrativeUnavailableReason = "No LLM provider is configured — add an API key to enable summaries.",
            };
        }

        if (!report.Stats.Sufficient || report.Analogs.Count == 0)
        {
            return report with
            {
                NarrativeUnavailableReason = "Not enough historical analogs to summarise reliably.",
            };
        }

        var systemPrompt =
            "You are an Elliott Wave analyst writing a two-sentence note comparing a setup to its "
            + "historical analogs. Use ONLY the numbers in the fact sheet — never state a rate, count "
            + "or date that is not listed. Respond ONLY with JSON: {\"narrative\": \"...\"}.";
        var language = await languageProvider.GetCurrentAsync(cancellationToken);
        if (NarrativeLanguageDirective.For(language) is { } languageDirective)
        {
            systemPrompt += " " + languageDirective;
        }

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, BuildFactSheet(report)),
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
            logger.LogWarning(ex, "Analog narrative generation failed");
            return report with { NarrativeUnavailableReason = "The summary service is temporarily unavailable." };
        }

        if (string.IsNullOrWhiteSpace(narrative))
        {
            return report with { NarrativeUnavailableReason = "The summary service returned no usable text." };
        }

        if (!AnalogFactGuard.Passes(narrative, report))
        {
            logger.LogWarning("Rejected analog narrative: cited a figure not in the report");
            return report with { NarrativeUnavailableReason = "The generated summary failed the fact check and was withheld." };
        }

        return report with { Narrative = narrative.Trim() };
    }

    private static string BuildFactSheet(AnalogReport report)
    {
        var stats = report.Stats;
        var sb = new StringBuilder();
        sb.Append("Concluded analogs: ").Append(stats.SampleCount).AppendLine();
        if (stats.HitRate is { } hit)
        {
            sb.Append("Reached target: ").Append(stats.TargetReached)
                .Append(" (").Append(Math.Round(hit * 100.0).ToString("0", CultureInfo.InvariantCulture)).AppendLine("%)");
        }

        sb.Append("Invalidated: ").Append(stats.Invalidated).AppendLine();
        if (stats.MedianResolutionDays is { } median)
        {
            sb.Append("Median days to resolution: ")
                .Append(Math.Round(median).ToString("0", CultureInfo.InvariantCulture)).AppendLine();
        }

        sb.AppendLine("Closest analogs:");
        foreach (var analog in report.Analogs.Take(5))
        {
            var setup = analog.Setup;
            sb.Append("- ").Append(setup.Features.Structure)
                .Append(setup.Features.Bullish ? " (bullish)" : " (bearish)")
                .Append(", ").Append(setup.Outcome)
                .Append(setup.ResolutionDays is { } days
                    ? $" after {Math.Round(days).ToString("0", CultureInfo.InvariantCulture)} days"
                    : string.Empty)
                .AppendLine();
        }

        return sb.ToString();
    }

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
