namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>The authenticated principal resolved from a session token.</summary>
public sealed record SessionPrincipal(Guid UserId, string Email);
