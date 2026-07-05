namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// One rung of a top-down chain: the best deterministic count found for a single timeframe,
/// constrained by the timeframe above it, plus the context it in turn imposes on the timeframe
/// below. All geometry is deterministic (grammar parser + rule checkers); no LLM is involved.
/// </summary>
/// <param name="Interval">The timeframe, e.g. "1W", "1D", "4H".</param>
/// <param name="Degree">Elliott degree assigned to this timeframe's top-level structure.</param>
/// <param name="BestCount">The best count for this timeframe after applying the parent constraint;
/// null only when the parent's direction admits no count here (a hard contradiction).</param>
/// <param name="ImposedContext">The constraint this timeframe hands down to the next finer one;
/// null when no count is available to derive it from.</param>
/// <param name="SearchTruncated">True when the parse hit its evaluation budget (coverage bounded,
/// counts still valid).</param>
public sealed record TimeframeCount(
    string Interval,
    WaveDegree Degree,
    WaveCandidate? BestCount,
    WaveContext? ImposedContext,
    bool SearchTruncated);
