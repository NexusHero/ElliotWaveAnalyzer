namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Request to add an analysis to the caller's track record. The frontend fills this from a
/// ranked count and its forward levels.
/// </summary>
public sealed record TrackAnalysisRequest(
    string Symbol,
    string Structure,
    bool Bullish,
    decimal? InvalidationPrice,
    bool InvalidationAbove,
    decimal? TargetLow,
    decimal? TargetHigh,
    string Confidence,
    decimal? Score);
