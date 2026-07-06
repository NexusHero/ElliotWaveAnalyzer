using System.Text;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Builds the prompt for the full-auto ("magic button") wave analysis. Unlike
/// <see cref="WaveValidationPromptBuilder"/> (which coaches a human's own count), this prompt
/// presents several machine-generated, rule-valid candidate counts and asks the model to pick
/// the most likely one and read the market structure — i.e. market analysis, not tutoring.
///
/// The model is told the candidate geometry is authoritative and must be referenced by id;
/// it must not invent prices. Provider-agnostic and pure (static, no I/O) so it can be
/// unit-tested without mocks.
/// </summary>
public static class AutoWaveAnalysisPromptBuilder
{
    public static string Build(
        string symbol,
        IReadOnlyList<MarketCandle> candles,
        IReadOnlyList<WaveCandidate> candidates)
    {
        var sb = new StringBuilder();

        AppendSystemRole(sb);
        AppendMarketContext(sb, symbol, candles);
        AppendCandidates(sb, candidates);
        AppendResponseSchema(sb);

        return sb.ToString();
    }

    private static void AppendSystemRole(StringBuilder sb) => sb.AppendLine("""
            You are an expert Elliott Wave market analyst. Below are several CANDIDATE wave
            counts for one instrument. Each candidate was detected algorithmically from the
            price swings and has already passed the canonical Elliott impulse rules — treat the
            pivots and prices as AUTHORITATIVE and do NOT invent or alter any prices.

            Your job:
            1. Decide which candidate is the most likely current count, given Elliott Wave
               theory, Fibonacci proportionality, and the market context.
            2. Give each candidate a short rationale and a confidence (high/medium/low).
            3. For each candidate, state the OUTLOOK the count implies under Elliott theory
               (e.g. "wave 5 appears complete, a corrective pullback toward the wave-4 area is
               typical next"). This is structural market analysis. Do NOT give explicit
               buy/sell/hold orders, price targets as trade instructions, or position sizing.
            Refer to candidates by their numeric id.
            """);

    private static void AppendMarketContext(
        StringBuilder sb, string symbol, IReadOnlyList<MarketCandle> candles)
    {
        if (candles.Count == 0)
        {
            return;
        }

        var ordered = candles.OrderBy(c => c.OpenTime).ToList();
        var low = candles.Min(c => c.Low);
        var high = candles.Max(c => c.High);
        var startPrice = ordered[0].Close;
        var endPrice = ordered[^1].Close;
        var overallChange = startPrice == 0 ? 0m : (endPrice - startPrice) / startPrice * 100m;

        sb.AppendLine($"""

            ## Market Context
            Symbol:       {symbol}
            Period:       {ordered[0].OpenTime:yyyy-MM-dd} – {ordered[^1].OpenTime:yyyy-MM-dd}
            Price range:  ${low:F2} (low) – ${high:F2} (high)
            Start price:  ${startPrice:F2}
            End price:    ${endPrice:F2}  ({overallChange:+0.00;-0.00}%)
            """);
    }

    private static void AppendCandidates(StringBuilder sb, IReadOnlyList<WaveCandidate> candidates)
    {
        sb.AppendLine();
        sb.AppendLine("## Candidate Wave Counts (AUTHORITATIVE geometry — reference by id)");

        foreach (var c in candidates)
        {
            sb.AppendLine();
            sb.Append($"### Candidate {c.Id} — {c.Structure} ({(c.RuleReport.BullishAssumed ? "bullish" : "bearish")})");
            sb.AppendLine(c.Score is { } score ? $" — guideline score {score:0.###}" : string.Empty);
            sb.AppendLine($"- origin: {c.Origin.Date:yyyy-MM-dd} @ ${c.Origin.Price:F2}");
            foreach (var w in c.Waves)
            {
                sb.AppendLine($"- wave {w.Label}: {w.Date:yyyy-MM-dd} @ ${w.Price:F2}");
            }

            foreach (var rule in c.RuleReport.Rules)
            {
                sb.AppendLine($"- [{rule.Status}] {rule.Name}");
            }

            if (c.RuleReport.Ratios.Count > 0)
            {
                foreach (var ratio in c.RuleReport.Ratios)
                {
                    sb.AppendLine($"- fib: {ratio.Name} = {ratio.Ratio:0.###}");
                }
            }

            if (c.Tree is { } tree && tree.Children.Any(ch => ch.Children.Count > 0))
            {
                sb.AppendLine("- internal subdivision (degree, structure, per-node score):");
                foreach (var child in tree.Children)
                {
                    AppendTree(sb, child, indent: 1);
                }
            }
        }
    }

    /// <summary>Renders one wave of the nested count, recursing into subdivided waves.
    /// Terminal legs stay one-line so the tree reads compactly.</summary>
    private static void AppendTree(StringBuilder sb, WaveNode node, int indent)
    {
        var pad = new string(' ', indent * 2);
        var subdivision = node.Kind is { } kind
            ? $" — {kind} at {node.Degree} degree, score {node.Score:0.###}"
            : string.Empty;
        sb.AppendLine(
            $"{pad}- wave {node.Label}: {node.Start.Date:yyyy-MM-dd} ${node.Start.Price:F2} → " +
            $"{node.End.Date:yyyy-MM-dd} ${node.End.Price:F2}{subdivision}");

        foreach (var child in node.Children)
        {
            AppendTree(sb, child, indent + 1);
        }
    }

    private static void AppendResponseSchema(StringBuilder sb) => sb.AppendLine("""

            ## Required Output
            Respond ONLY with valid JSON — no markdown fences, no prose before or after.
            Use exactly this schema:

            {
              "bestCandidateId": <int>,
              "marketSummary": "string — one paragraph on the overall structure & most likely scenario",
              "rankings": [
                {
                  "candidateId": <int>,
                  "confidence": "high" | "medium" | "low",
                  "rationale": "string — why this count fits or doesn't",
                  "outlook": "string — what the count implies for the likely next move (no trade orders)"
                }
              ]
            }

            - Include every candidate id exactly once in "rankings", most likely first.
            - "bestCandidateId" MUST be one of the candidate ids shown above.
            """);
}
