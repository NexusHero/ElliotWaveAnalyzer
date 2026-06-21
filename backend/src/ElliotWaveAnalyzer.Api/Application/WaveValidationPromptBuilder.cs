using System.Text;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Builds the structured text prompt for Elliott Wave validation. Provider-agnostic:
/// the same prompt is sent regardless of the backing LLM (Gemini/Claude/OpenAI).
///
/// WHY this lives in the Application layer (not Infrastructure):
/// it encodes the Elliott Wave rules and the validation contract — pure business
/// logic with no I/O and no SDK dependency. Keeping it provider-neutral and here
/// means a new LLM provider never touches it.
///
/// WHY text prompt instead of an image:
/// Sending price data as structured text is more precise (exact prices/dates),
/// cheaper (no multimodal tokens), and gives the model better grounding than reading
/// numbers off a chart image.
///
/// Intentionally pure (static, no I/O, no dependencies) so it can be tested
/// exhaustively without mocks. The prompt is logic — it deserves unit tests.
/// </summary>
public static class WaveValidationPromptBuilder
{
    private static readonly string[] ElliottWaveRules =
    [
        "Rule 1 — Wave 2 must not retrace beyond the origin (start) of Wave 1.",
        "Rule 2 — Wave 3 must never be the shortest of the three impulse waves (1, 3, 5).",
        "Rule 3 — Wave 4 must not overlap (enter the price territory of) Wave 1, except in diagonal triangles.",
        "Guideline — Wave 5 often extends to or slightly beyond the end of Wave 3.",
        "Guideline — Wave 2 commonly retraces 50–61.8% of Wave 1.",
        "Guideline — Wave 4 commonly retraces 38.2% of Wave 3.",
        "Guideline — In a corrective ABC: Wave B should retrace into Wave A's territory; Wave C should end beyond Wave A's end.",
    ];

    /// <summary>
    /// Builds the complete prompt for a wave validation request.
    /// </summary>
    /// <param name="symbol">Ticker symbol for context.</param>
    /// <param name="candles">Full candle set covering (at minimum) the annotated period.</param>
    /// <param name="annotations">User-placed wave labels. Will be sorted chronologically.</param>
    public static string Build(
        string symbol,
        IReadOnlyList<MarketCandle> candles,
        IReadOnlyList<WaveAnnotation> annotations,
        WaveRuleReport ruleReport)
    {
        var sorted = annotations.OrderBy(a => a.Date).ToList();
        var sb = new StringBuilder();

        AppendSystemRole(sb);
        AppendMarketContext(sb, symbol, candles);
        AppendAnnotations(sb, sorted);
        AppendRules(sb);
        AppendDeterministicChecks(sb, ruleReport);
        AppendResponseSchema(sb);

        return sb.ToString();
    }

    private static void AppendDeterministicChecks(StringBuilder sb, WaveRuleReport report)
    {
        sb.AppendLine("## Deterministic Checks (AUTHORITATIVE — already computed, do NOT re-derive)");
        sb.AppendLine($"Assumed direction: {(report.BullishAssumed ? "bullish (up)" : "bearish (down)")}");
        foreach (var rule in report.Rules)
        {
            sb.AppendLine($"- [{rule.Status}] {rule.Name} — {rule.Detail}");
        }
        if (report.Ratios.Count > 0)
        {
            sb.AppendLine("Fibonacci ratios:");
            foreach (var ratio in report.Ratios)
            {
                sb.AppendLine($"- {ratio.Name}: {ratio.Ratio:0.###}");
            }
        }
        sb.AppendLine();
    }

    // ─── Sections ─────────────────────────────────────────────────────────────

    private static void AppendSystemRole(StringBuilder sb) => sb.AppendLine("""
            You are an Elliott Wave coach helping a trader LEARN, not a signal service.
            The objective rule checks have already been computed deterministically and are
            given below as authoritative — trust them; do not re-derive rule pass/fail.
            Your job is the qualitative layer: explain WHY in plain terms, point out the
            Fibonacci context, suggest one plausible ALTERNATIVE count, and ask 1–2 short
            reflective questions that help the trader reason for themselves.
            Refer to specific wave labels and prices. Be concise and educational.
            Hard guardrail: NEVER give buy/sell/hold advice, price predictions, or position
            sizing. This is wave-structure education only.
            """);

    private static void AppendMarketContext(
        StringBuilder sb, string symbol, IReadOnlyList<MarketCandle> candles)
    {
        if (candles.Count == 0)
        {
            return;
        }

        var low = candles.Min(c => c.Low);
        var high = candles.Max(c => c.High);
        var start = candles.Min(c => c.OpenTime);
        var end = candles.Max(c => c.OpenTime);
        var startPrice = candles.OrderBy(c => c.OpenTime).First().Close;
        var endPrice = candles.OrderBy(c => c.OpenTime).Last().Close;
        var overallChange = (endPrice - startPrice) / startPrice * 100m;

        sb.AppendLine($"""
            ## Market Context
            Symbol:       {symbol}/USD
            Period:       {start:yyyy-MM-dd} – {end:yyyy-MM-dd}
            Price range:  ${low:F2} (low) – ${high:F2} (high)
            Start price:  ${startPrice:F2}
            End price:    ${endPrice:F2}  ({overallChange:+0.00;-0.00}%)
            """);
    }

    private static void AppendAnnotations(
        StringBuilder sb, List<WaveAnnotation> sorted)
    {
        sb.AppendLine("## Wave Annotations (chronological)");
        sb.AppendLine("| Wave | Date       | Price ($)     | Δ from prev wave    |");
        sb.AppendLine("|------|------------|---------------|---------------------|");

        for (var i = 0; i < sorted.Count; i++)
        {
            var ann = sorted[i];
            var delta = i == 0
                ? "—"
                : FormatDelta(sorted[i - 1].Price, ann.Price);

            sb.AppendLine($"| {ann.Label,-4} | {ann.Date:yyyy-MM-dd} | {ann.Price,13:F2} | {delta,-20} |");
        }

        sb.AppendLine();
    }

    private static void AppendRules(StringBuilder sb)
    {
        sb.AppendLine("## Elliott Wave Rules & Guidelines to Apply");
        foreach (var rule in ElliottWaveRules)
        {
            sb.AppendLine($"- {rule}");
        }

        sb.AppendLine();
    }

    private static void AppendResponseSchema(StringBuilder sb) => sb.AppendLine("""
            ## Required Output
            Respond ONLY with valid JSON — no markdown fences, no prose before or after.
            Use exactly this schema:

            {
              "isValid": true | false,
              "violations": ["string", ...],
              "warnings": ["string", ...],
              "analysis": "string (the coaching reflection)",
              "confidence": "high" | "medium" | "low"
            }

            - "isValid" / "violations" MUST mirror the authoritative deterministic checks
              above (isValid = true only when no rule has status Fail). Do not contradict them.
            - "warnings" are guideline deviations or ambiguous patterns (e.g. unusual
              Fibonacci ratios, a possible diagonal).
            - "analysis" is your coaching: a short plain-language explanation, the Fibonacci
              context, ONE plausible alternative count, and 1–2 reflective questions for the
              trader. No trading advice.
            - "confidence" reflects how certain the wave count is given the available pivots.
            """);

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static string FormatDelta(decimal from, decimal to)
    {
        var abs = to - from;
        var pct = from == 0 ? 0m : (to - from) / from * 100m;
        return $"{abs:+0.00;-0.00} ({pct:+0.0;-0.0}%)";
    }
}
