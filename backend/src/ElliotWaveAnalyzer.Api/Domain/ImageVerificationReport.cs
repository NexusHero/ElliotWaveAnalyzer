namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The result of verifying an uploaded analyst chart: what the vision model extracted, which claimed
/// pivots snapped to real candles and which were rejected as hallucinations, the deterministic rule
/// verdicts on the snapped count, and a side-by-side comparison with our own count. When too few
/// pivots snap, <see cref="Status"/> is <see cref="ImageVerificationStatus.ExtractionUnreliable"/> and
/// no rule verdicts are fabricated. LLM for perception, rules for judgment (ADR-009).
/// </summary>
/// <param name="Status">Whether the image was reliably extracted.</param>
/// <param name="Extraction">The raw claim from the vision model.</param>
/// <param name="Snapped">Claimed pivots that snapped to real candle extremes.</param>
/// <param name="Rejected">Claimed pivots that did not snap (with reasons).</param>
/// <param name="ClaimedRules">Rule verdicts on the snapped count; null when extraction was unreliable.</param>
/// <param name="Comparison">Comparison with our own count; null when extraction was unreliable.</param>
/// <param name="Message">A human-readable summary of the outcome.</param>
public sealed record ImageVerificationReport(
    ImageVerificationStatus Status,
    ChartExtraction Extraction,
    IReadOnlyList<SnappedPivot> Snapped,
    IReadOnlyList<RejectedPivot> Rejected,
    WaveRuleReport? ClaimedRules,
    CountComparison? Comparison,
    string Message);
