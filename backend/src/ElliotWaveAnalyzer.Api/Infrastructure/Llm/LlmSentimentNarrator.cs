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
/// Summarises a deterministic <see cref="SentimentReport"/> via the configured <see cref="IChatClient"/>.
/// The model receives only the computed mood series and divergences and is told to cite only them; its
/// output is then run through <see cref="SentimentFactGuard"/>, so a summary that invents a reading is
/// withheld rather than shown. Degrades gracefully: no chat client (no key), no sentiment coverage, or
/// a transport/parse failure returns the report unchanged with an explicit reason — the deterministic
/// read always stands on its own. Mirrors <see cref="LlmAnalogNarrator"/> exactly.
/// </summary>
internal sealed class LlmSentimentNarrator(
    IEnumerable<IChatClient> chatClients,
    IOptions<LlmProviderOptions> options,
    ILogger<LlmSentimentNarrator> logger) : ISentimentNarrator
{
    private const int MaxOutputTokens = 300;
    private readonly IChatClient? _chatClient = chatClients.FirstOrDefault();

    /// <inheritdoc/>
    public async Task<SentimentReport> NarrateAsync(
        SentimentReport report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (_chatClient is null)
        {
            return report with
            {
                NarrativeUnavailableReason = "No LLM provider is configured — add an API key to enable summaries.",
            };
        }

        if (!report.HasCoverage)
        {
            return report with
            {
                NarrativeUnavailableReason = "No sentiment coverage for this symbol.",
            };
        }

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System,
                "You are an Elliott Wave analyst writing a two-sentence note on social mood versus wave "
                + "position (socionomics). Use ONLY the numbers in the fact sheet — never state a mood "
                + "score that is not listed. Respond ONLY with JSON: {\"narrative\": \"...\"}."),
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
            logger.LogWarning(ex, "Sentiment narrative generation failed");
            return report with { NarrativeUnavailableReason = "The summary service is temporarily unavailable." };
        }

        if (string.IsNullOrWhiteSpace(narrative))
        {
            return report with { NarrativeUnavailableReason = "The summary service returned no usable text." };
        }

        if (!SentimentFactGuard.Passes(narrative, report))
        {
            logger.LogWarning("Rejected sentiment narrative: cited a mood score not in the report");
            return report with { NarrativeUnavailableReason = "The generated summary failed the fact check and was withheld." };
        }

        return report with { Narrative = narrative.Trim() };
    }

    private static string BuildFactSheet(SentimentReport report)
    {
        var sb = new StringBuilder();
        sb.Append("Mood readings (date: score in [-1, 1]):").AppendLine();
        foreach (var point in report.Series)
        {
            sb.Append("- ").Append(point.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Append(": ").Append(point.Score.ToString("0.00", CultureInfo.InvariantCulture)).AppendLine();
        }

        if (report.Divergences.Count == 0)
        {
            sb.AppendLine("No divergences detected.");
        }
        else
        {
            sb.AppendLine("Divergences:");
            foreach (var d in report.Divergences)
            {
                sb.Append("- wave ").Append(d.PivotLabel).Append(": ").Append(d.Kind)
                    .Append(" (earlier mood ").Append(d.EarlierMood.ToString("0.00", CultureInfo.InvariantCulture))
                    .Append(", later mood ").Append(d.LaterMood.ToString("0.00", CultureInfo.InvariantCulture))
                    .AppendLine(")");
            }
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
