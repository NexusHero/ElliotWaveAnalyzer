namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>Outcome of a login attempt. On success carries the opaque session token.</summary>
public sealed record SessionResult(bool Succeeded, string? Token, DateTimeOffset? ExpiresAt, string? Error);
