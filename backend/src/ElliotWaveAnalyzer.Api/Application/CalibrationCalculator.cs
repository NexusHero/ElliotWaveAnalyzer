using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Pure calculation of confidence calibration from recorded outcomes: buckets analyses by their
/// (normalized) confidence label and, per bucket, counts how many concluded and how many of those
/// reached their target, yielding a hit rate. Pending analyses count toward <c>Total</c> but not
/// <c>Concluded</c> — a hit rate is only meaningful over settled calls.
///
/// Kept pure/static (no I/O) so the maths is exhaustively unit-testable without a database.
/// </summary>
public static class CalibrationCalculator
{
    /// <summary>Canonical bucket order; any other confidence label sorts after these, alphabetically.</summary>
    private static readonly string[] Order = ["high", "medium", "low"];

    /// <summary>
    /// Builds the calibration from (confidence, outcome) pairs. Confidence labels are compared
    /// case-insensitively and normalized to lower case; blank confidences bucket as "unknown".
    /// </summary>
    public static ConfidenceCalibration Calculate(IEnumerable<(string Confidence, AnalysisOutcome Outcome)> analyses)
    {
        ArgumentNullException.ThrowIfNull(analyses);

        var buckets = analyses
            .GroupBy(a => Normalize(a.Confidence))
            .Select(g =>
            {
                var total = g.Count();
                var targetReached = g.Count(a => a.Outcome == AnalysisOutcome.TargetReached);
                var invalidated = g.Count(a => a.Outcome == AnalysisOutcome.Invalidated);
                var concluded = targetReached + invalidated;
                return new CalibrationBucket(
                    g.Key, total, concluded, targetReached, invalidated, HitRate(targetReached, concluded));
            })
            .OrderBy(b => RankOf(b.Confidence))
            .ThenBy(b => b.Confidence, StringComparer.Ordinal)
            .ToList();

        var totalConcluded = buckets.Sum(b => b.Concluded);
        var totalTargets = buckets.Sum(b => b.TargetReached);

        return new ConfidenceCalibration(buckets, totalConcluded, HitRate(totalTargets, totalConcluded));
    }

    private static string Normalize(string confidence)
        => string.IsNullOrWhiteSpace(confidence) ? "unknown" : confidence.Trim().ToLowerInvariant();

    private static int RankOf(string confidence)
    {
        var index = Array.IndexOf(Order, confidence);
        return index >= 0 ? index : Order.Length; // unknown/other after the canonical three
    }

    private static decimal? HitRate(int targetReached, int concluded)
        => concluded == 0 ? null : Math.Round((decimal)targetReached / concluded, 3);
}
