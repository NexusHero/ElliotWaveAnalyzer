namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A claimed pivot that did <b>not</b> snap to any real candle extreme within tolerance — a likely
/// hallucination — with a human-readable reason, so the report can say exactly what it rejected and why.
/// </summary>
/// <param name="Label">The Elliott label from the claim.</param>
/// <param name="ApproxDate">The date the model read.</param>
/// <param name="ApproxPrice">The price the model read.</param>
/// <param name="Reason">Why it was rejected.</param>
public sealed record RejectedPivot(string Label, DateTime ApproxDate, decimal ApproxPrice, string Reason);
