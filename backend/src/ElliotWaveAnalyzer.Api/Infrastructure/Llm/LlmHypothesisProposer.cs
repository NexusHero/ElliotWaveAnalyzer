using System.Globalization;
using System.Text;
using System.Text.Json;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Llm;

/// <summary>
/// Proposes Elliott structure hypotheses via the configured <see cref="IChatClient"/>. The model is
/// given only a compact description of the detected pivots and the allowed vocabulary, and is asked to
/// <b>name</b> structures worth testing with a one-line reason each — never to assert a count is valid
/// (the engine decides that). It returns at most <c>max</c> proposals; anything it invents outside the
/// vocabulary is dropped downstream. With no chat client configured the feature is off.
/// </summary>
internal sealed class LlmHypothesisProposer(
    IEnumerable<IChatClient> chatClients,
    IOptions<LlmProviderOptions> options,
    ILogger<LlmHypothesisProposer> logger) : IHypothesisProposer
{
    private const int MaxOutputTokens = 400;
    private readonly IChatClient? _chatClient = chatClients.FirstOrDefault();

    /// <inheritdoc/>
    public bool IsConfigured => _chatClient is not null;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RawHypothesis>> ProposeAsync(
        string symbol, IReadOnlyList<SwingPivot> pivots, int max, CancellationToken cancellationToken = default)
    {
        if (_chatClient is null || pivots.Count < 3)
        {
            return [];
        }

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System,
                "You are an Elliott Wave analyst suggesting which STRUCTURES are worth testing for a set of "
                + "pivots. You do NOT decide validity — a deterministic engine will rule-check each. Choose only "
                + "from this vocabulary: impulse, diagonal, zigzag, flat, triangle. Give at most " + max
                + " suggestions, each with a one-line reason. Respond ONLY with JSON: "
                + "{\"proposals\":[{\"structure\":\"...\",\"reason\":\"...\"}]}."),
            new(ChatRole.User, Describe(symbol, pivots)),
        };

        var chatOptions = new ChatOptions
        {
            ModelId = options.Value.GetActiveEndpoint().Model,
            MaxOutputTokens = MaxOutputTokens,
            ResponseFormat = ChatResponseFormat.Json,
        };

        try
        {
            var response = await _chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
            return Parse(response.Text, max);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Hypothesis proposal failed for {Symbol}", symbol);
            return [];
        }
    }

    private static string Describe(string symbol, IReadOnlyList<SwingPivot> pivots)
    {
        var ordered = pivots.OrderBy(p => p.Date).ToList();
        var up = ordered[^1].Price > ordered[0].Price;
        var sb = new StringBuilder();
        sb.Append("Symbol: ").AppendLine(symbol);
        sb.Append(ordered.Count).Append(" swing pivots detected, net ")
            .AppendLine(up ? "upward." : "downward.");
        sb.AppendLine("Recent pivots (oldest→newest), high (H) / low (L):");
        foreach (var pivot in ordered.TakeLast(8))
        {
            sb.Append("  ").Append(pivot.IsHigh ? "H " : "L ")
                .AppendLine(pivot.Price.ToString("0.####", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    private static IReadOnlyList<RawHypothesis> Parse(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(text[start..(end + 1)]);
            if (!doc.RootElement.TryGetProperty("proposals", out var arr) || arr.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var results = new List<RawHypothesis>();
            foreach (var item in arr.EnumerateArray())
            {
                var structure = item.TryGetProperty("structure", out var s) ? s.GetString() : null;
                if (string.IsNullOrWhiteSpace(structure))
                {
                    continue;
                }

                var reason = item.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "";
                results.Add(new RawHypothesis(structure.Trim(), reason.Trim()));
                if (results.Count >= max)
                {
                    break;
                }
            }

            return results;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
