namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The full deterministic read of an analyst-edited wave count (REQ-031): the pivots snapped to real
/// candle extremes, the hard-rule report, the forward projections/zones, and a guideline score — all
/// computed in code, no LLM. Returned on every edit so the analyst-in-the-loop sees the objective
/// verdict of their own count live. The LLM is only offered afterwards to narrate; it never edits.
/// </summary>
/// <param name="Structure">Inferred structure family from the drawn labels ("Impulse"/"Corrective"/"Unknown").</param>
/// <param name="Bullish">True when the count reads directionally up.</param>
/// <param name="IsValid">True when no <b>hard</b> rule failed (guideline failures don't invalidate).</param>
/// <param name="Snapped">The edited pivots that snapped to a real candle extreme (what was verified).</param>
/// <param name="Rejected">Edited pivots that did not land on a candle within tolerance — flagged, not trusted.</param>
/// <param name="Rules">The deterministic hard-rule report (+ key Fibonacci ratios) on the snapped count.</param>
/// <param name="Levels">Forward projections: invalidation, support/target zones, confluence, channels; null if too few pivots.</param>
/// <param name="Score">Guideline score in [0,1] for the count's structure; null when it can't be scored.</param>
public sealed record WaveVerification(
    string Structure,
    bool Bullish,
    bool IsValid,
    IReadOnlyList<SnappedPivot> Snapped,
    IReadOnlyList<RejectedPivot> Rejected,
    WaveRuleReport Rules,
    WaveLevels? Levels,
    double? Score);
