namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>
/// Explicit confirmation for account deletion (#168 AC2). Required only when the account has a
/// password to re-confirm; an OAuth-only account has none, so this may be omitted for those.
/// </summary>
public sealed record DeleteAccountRequest(string? CurrentPassword);
