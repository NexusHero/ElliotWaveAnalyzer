namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The consistency verdict for one adjacent parent→child link in a top-down chain, with a
/// human-readable reason so the disagreement (or agreement) is explainable.
/// </summary>
/// <param name="ParentInterval">Coarser timeframe, e.g. "1W".</param>
/// <param name="ChildInterval">Finer timeframe, e.g. "1D".</param>
/// <param name="Verdict">How well the child fits inside the parent's active wave.</param>
/// <param name="Reason">Why this verdict was reached.</param>
public sealed record TimeframeConsistency(
    string ParentInterval,
    string ChildInterval,
    ConsistencyVerdict Verdict,
    string Reason);
