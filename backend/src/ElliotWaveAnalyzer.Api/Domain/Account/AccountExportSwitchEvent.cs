namespace ElliotWaveAnalyzer.Api.Domain.Account;

/// <summary>One auto-switch event from an exported analysis's append-only switch history.</summary>
public sealed record AccountExportSwitchEvent(DateTimeOffset At, string FromLabel, string ToLabel, string Reason);
