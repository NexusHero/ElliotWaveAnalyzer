using System.Security.Cryptography;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// Authentication service built on ASP.NET Core Identity (credentials + hashing) and a
/// server-side opaque-session store. Tokens are 256-bit random values; only their
/// SHA-256 hash is persisted. Login uses Identity's lockout to throttle brute force and
/// returns a generic error to avoid account enumeration.
/// </summary>
public sealed class AuthService(
    UserManager<AppUser> userManager,
    AppDbContext db,
    IOptions<AuthOptions> options,
    TimeProvider timeProvider,
    ILogger<AuthService> logger) : IAuthService
{
    private const string GenericLoginError = "Invalid email or password.";

    /// <inheritdoc/>
    public async Task<AuthResult> RegisterAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = new AppUser { UserName = email, Email = email };
        var result = await userManager.CreateAsync(user, password);

        if (result.Succeeded)
        {
            // Log the non-PII user id, never the email (cs/exposure-of-sensitive-information).
            logger.LogInformation("Registered new user {UserId}", user.Id);
            return new AuthResult(true, []);
        }

        return new AuthResult(false, [.. result.Errors.Select(e => e.Description)]);
    }

    /// <inheritdoc/>
    public async Task<SessionResult> LoginAsync(
        string email, string password, string? ip, string? userAgent, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return new SessionResult(false, null, null, GenericLoginError);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return new SessionResult(false, null, null, "Account is temporarily locked. Try again later.");
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            return new SessionResult(false, null, null, GenericLoginError);
        }

        await userManager.ResetAccessFailedCountAsync(user);

        return await CreateSessionAsync(user, ip, userAgent, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SessionResult> ExternalLoginAsync(
        string email, bool emailVerified, string? ip, string? userAgent, CancellationToken cancellationToken = default)
    {
        // Never trust an unverified external email: it could be an attacker-controlled
        // account asserting someone else's address, which would take over their account.
        if (!emailVerified)
        {
            logger.LogWarning("Rejected external login with an unverified email");
            return new SessionResult(false, null, null, GenericLoginError);
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            // Just-in-time provisioning: the external provider has verified the email, so
            // we create a passwordless account on first sign-in. EmailConfirmed is true
            // because the provider already confirmed ownership.
            user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
            var create = await userManager.CreateAsync(user);
            if (!create.Succeeded)
            {
                return new SessionResult(false, null, null, string.Join(" ", create.Errors.Select(e => e.Description)));
            }

            logger.LogInformation("Provisioned new user {UserId} from external login", user.Id);
        }

        return await CreateSessionAsync(user, ip, userAgent, cancellationToken);
    }

    /// <summary>Issues an opaque server-side session for an already-authenticated user.</summary>
    private async Task<SessionResult> CreateSessionAsync(
        AppUser user, string? ip, string? userAgent, CancellationToken cancellationToken)
    {
        var token = GenerateToken();
        var now = timeProvider.GetUtcNow();
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = Hash(token),
            CreatedAt = now,
            ExpiresAt = now.AddHours(options.Value.SessionLifetimeHours),
            CreatedByIp = ip,
            UserAgent = userAgent,
        };

        db.Sessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        // Log the non-PII user id, never the email (cs/exposure-of-sensitive-information).
        logger.LogInformation("User {UserId} logged in; session {SessionId} created", user.Id, session.Id);
        return new SessionResult(true, token, session.ExpiresAt, null);
    }

    /// <inheritdoc/>
    public async Task<SessionPrincipal?> ValidateSessionAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var hash = Hash(token);
        var now = timeProvider.GetUtcNow();

        var session = await db.Sessions
            .FirstOrDefaultAsync(s => s.TokenHash == hash && s.RevokedAt == null && s.ExpiresAt > now, cancellationToken);
        if (session is null)
        {
            return null;
        }

        var user = await userManager.FindByIdAsync(session.UserId.ToString());
        return user?.Email is null ? null : new SessionPrincipal(user.Id, user.Email);
    }

    /// <inheritdoc/>
    public async Task LogoutAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var hash = Hash(token);
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.TokenHash == hash, cancellationToken);
        if (session is { RevokedAt: null })
        {
            session.RevokedAt = timeProvider.GetUtcNow();
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    private static string Hash(string token)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }
}
