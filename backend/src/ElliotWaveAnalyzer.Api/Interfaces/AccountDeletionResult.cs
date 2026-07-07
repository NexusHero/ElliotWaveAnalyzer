namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>Outcome of an account-deletion attempt.</summary>
public sealed record AccountDeletionResult(bool Succeeded, string? Error);
