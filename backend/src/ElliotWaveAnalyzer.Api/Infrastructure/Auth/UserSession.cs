namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// A server-side session. The opaque token handed to the client is never stored —
/// only its SHA-256 hash, so a database leak does not expose usable tokens.
/// </summary>
internal sealed class UserSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>SHA-256 (hex) of the opaque session token. Never store the token itself.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }

    /// <summary>A session is usable only while it is neither revoked nor expired.</summary>
    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
