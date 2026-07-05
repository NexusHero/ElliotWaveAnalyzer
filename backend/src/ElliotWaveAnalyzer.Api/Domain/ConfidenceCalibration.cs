namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Whether the AI's confidence has held up: per-confidence buckets plus the overall hit rate.
/// Computed on read from the track record — no stored state — so it always reflects live outcomes.
/// </summary>
/// <param name="Buckets">Per-confidence rows, ordered high → medium → low → other.</param>
/// <param name="TotalConcluded">All concluded analyses across every bucket.</param>
/// <param name="OverallHitRate">Target-reached ÷ concluded across all buckets; null when none concluded.</param>
public sealed record ConfidenceCalibration(
    IReadOnlyList<CalibrationBucket> Buckets,
    int TotalConcluded,
    decimal? OverallHitRate);
