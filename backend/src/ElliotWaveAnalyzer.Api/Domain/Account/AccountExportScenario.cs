namespace ElliotWaveAnalyzer.Api.Domain.Account;

/// <summary>One scenario (primary or alternate) of an exported analysis's tree.</summary>
public sealed record AccountExportScenario(
    string Role,
    int OrderIndex,
    string Label,
    string Structure,
    bool Bullish,
    decimal? InvalidationPrice,
    bool InvalidationAbove,
    decimal? EntryLow,
    decimal? EntryHigh,
    decimal? TargetLow,
    decimal? TargetHigh,
    string Confidence,
    decimal? Score,
    bool Retired);
