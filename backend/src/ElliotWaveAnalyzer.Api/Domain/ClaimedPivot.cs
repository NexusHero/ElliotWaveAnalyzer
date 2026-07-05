namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A pivot the vision model claims to have read off an uploaded chart: an approximate date and price
/// and the Elliott label drawn next to it. "Approximate" because it comes from perception, not data —
/// it must be snapped to a real candle extreme before any rule is applied (the hallucination guard).
/// </summary>
/// <param name="ApproxDate">The date the model read for this pivot.</param>
/// <param name="ApproxPrice">The price the model read for this pivot.</param>
/// <param name="Label">The Elliott label drawn at this pivot (e.g. "0"–"5", "A"–"C").</param>
public sealed record ClaimedPivot(DateTime ApproxDate, decimal ApproxPrice, string Label);
