namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>
/// Request body for <c>POST /api/wave-analysis/auto</c>. Only <see cref="Symbol"/> is
/// required; the rest fall back to sensible, clamped defaults server-side.
/// </summary>
public sealed record AutoWaveAnalysisRequest(
    string Symbol,
    int? LookbackDays = null,
    decimal? ThresholdPercent = null);
