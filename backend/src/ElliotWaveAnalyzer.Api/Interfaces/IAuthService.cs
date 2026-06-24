namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>Outcome of a registration attempt.</summary>
public sealed record AuthResult(bool Succeeded, IReadOnlyList<string> Errors);

/// <summary>Outcome of a login attempt. On success carries the opaque session token.</summary>
public sealed record SessionResult(bool Succeeded, string? Token, DateTimeOffset? ExpiresAt, string? Error);

/// <summary>The authenticated principal resolved from a session token.</summary>
public sealed record SessionPrincipal(Guid UserId, string Email);

/// <summary>
/// Authentication operations: account creation, login (issuing an opaque server-side
/// session), session validation, and logout.
/// </summary>
public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<SessionResult> LoginAsync(
        string email, string password, string? ip, string? userAgent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs in a user authenticated by an external provider (e.g. Google). The provider
    /// has already verified the identity, so there is no password: the account is created
    /// on first sign-in (just-in-time provisioning) and an opaque session is issued.
    /// </summary>
    Task<SessionResult> ExternalLoginAsync(
        string email, string? ip, string? userAgent, CancellationToken cancellationToken = default);

    Task<SessionPrincipal?> ValidateSessionAsync(string token, CancellationToken cancellationToken = default);

    Task LogoutAsync(string token, CancellationToken cancellationToken = default);
}
