namespace ElliotWaveAnalyzer.Api.Domain.Account;

/// <summary>The account's own identity fields — nothing beyond what Identity itself stores.</summary>
public sealed record AccountExportProfile(Guid Id, string Email, bool EmailConfirmed);
