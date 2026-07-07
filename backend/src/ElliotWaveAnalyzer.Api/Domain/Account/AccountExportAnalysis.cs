namespace ElliotWaveAnalyzer.Api.Domain.Account;

/// <summary>One saved wave analysis, including its full scenario tree and switch history.</summary>
public sealed record AccountExportAnalysis(
    Guid Id,
    string Symbol,
    DateTimeOffset CreatedAt,
    string Structure,
    bool Bullish,
    decimal? InvalidationPrice,
    bool InvalidationAbove,
    decimal? TargetLow,
    decimal? TargetHigh,
    decimal? EntryLow,
    decimal? EntryHigh,
    string Confidence,
    decimal? Score,
    string AlertedOutcome,
    bool EntryZoneAlerted,
    IReadOnlyList<AccountExportScenario> Scenarios,
    IReadOnlyList<AccountExportSwitchEvent> SwitchEvents);
