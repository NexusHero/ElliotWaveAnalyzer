using System.Text;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Gemini;

/// <summary>
/// Builds the structured text prompt sent to Gemini for Elliott Wave validation.
///
/// WHY text prompt instead of image:
/// Sending price data as structured text is more precise (exact prices, exact dates),
/// cheaper (no multimodal tokens), and gives Gemini better grounding than reading
/// numbers off a chart image. The LLM's reasoning about wave ratios is more reliable
/// when working with numeric data directly.
///
/// This class is intentionally pure (static, no I/O, no dependencies) so it can be
/// tested exhaustively without mocks. The prompt is logic — it deserves unit tests.
/// </summary>
internal static class GeminiPromptBuilder
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
    /// Builds the complete Gemini prompt for a wave validation request.
    /// </summary>
    /// <param name="symbol">Ticker symbol for context.</param>
    /// <param name="candles">Full candle set covering (at minimum) the annotated period.</param>
    /// <param name="annotations">User-placed wave labels. Will be sorted chronologically.</param>
    public static string Build(
        string symbol,
        IReadOnlyList<MarketCandle> candles,
        IReadOnlyList<WaveAnnotation> annotations)
    {
        var sorted = annotations.OrderBy(a => a.Date).ToList();
        var sb = new StringBuilder();

        AppendSystemRole(sb);
        AppendMarketContext(sb, symbol, candles);
        AppendAnnotations(sb, sorted);
        AppendRules(sb);
        AppendResponseSchema(sb);

        return sb.ToString();
    }

    // ─── Sections ─────────────────────────────────────────────────────────────

    private static void AppendSystemRole(StringBuilder sb)
    {
        sb.AppendLine("""
            You are an expert technical analyst specializing in Elliott Wave Theory.
            Your task is to validate a user-drawn Elliott Wave count against the canonical rules.
            Be precise, refer to specific wave labels and prices when citing violations.
            """);
    }

    private static void AppendMarketContext(
        StringBuilder sb, string symbol, IReadOnlyList<MarketCandle> candles)
    {
        if (candles.Count == 0) return;

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
            sb.AppendLine($"- {rule}");
        sb.AppendLine();
    }

    private static void AppendResponseSchema(StringBuilder sb)
    {
        sb.AppendLine("""
            ## Required Output
            Respond ONLY with valid JSON — no markdown fences, no prose before or after.
            Use exactly this schema:

            {
              "isValid": true | false,
              "violations": ["string", ...],
              "warnings": ["string", ...],
              "analysis": "string (2–4 sentence overall assessment)",
              "confidence": "high" | "medium" | "low"
            }

            - "isValid" is true only when "violations" is empty.
            - "violations" are hard rule breaches (Rules 1–3 above).
            - "warnings" are guideline deviations or ambiguous patterns.
            - "confidence" reflects how certain you are of your assessment
              given the available data (e.g. "low" if fewer than 3 waves are annotated).
            """);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static string FormatDelta(decimal from, decimal to)
    {
        var abs = to - from;
        var pct = from == 0 ? 0m : (to - from) / from * 100m;
        return $"{abs:+0.00;-0.00} ({pct:+0.0;-0.0}%)";
    }
}
