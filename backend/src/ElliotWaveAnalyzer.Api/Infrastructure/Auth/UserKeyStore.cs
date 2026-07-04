using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// <see cref="IUserKeyStore"/> backed by EF Core + ASP.NET Core Data Protection. Keys are
/// encrypted with a purpose-scoped <see cref="IDataProtector"/> before they touch the database
/// and decrypted only when handed to the provider. Only the ciphertext and the last four
/// characters are stored; the plaintext is never persisted and never leaves through an endpoint.
/// </summary>
internal sealed class UserKeyStore : IUserKeyStore
{
    /// <summary>Providers a key may be stored for (matches the keyed LLM clients).</summary>
    private static readonly HashSet<string> KnownProviders =
        new(StringComparer.OrdinalIgnoreCase) { "gemini", "claude", "openai" };

    private readonly AppDbContext _db;
    private readonly IDataProtector _protector;
    private readonly TimeProvider _timeProvider;

    public UserKeyStore(AppDbContext db, IDataProtectionProvider protectionProvider, TimeProvider timeProvider)
    {
        _db = db;
        // Purpose string scopes the key ring so these ciphertexts can't be decrypted by another feature.
        _protector = protectionProvider.CreateProtector("ElliotWaveAnalyzer.UserApiKey.v1");
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async Task<SavedApiKey> SaveAsync(
        Guid userId, string provider, string plaintextKey, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(provider);
        if (string.IsNullOrWhiteSpace(plaintextKey))
        {
            throw new ArgumentException("The API key must not be empty.", nameof(plaintextKey));
        }

        var key = plaintextKey.Trim();
        var existing = await _db.UserApiKeys
            .FirstOrDefaultAsync(k => k.UserId == userId && k.Provider == normalized, cancellationToken);
        var isFirst = !await _db.UserApiKeys.AnyAsync(k => k.UserId == userId, cancellationToken);

        if (existing is null)
        {
            existing = new UserApiKey
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Provider = normalized,
                CreatedAt = _timeProvider.GetUtcNow(),
                IsDefault = isFirst, // the very first key a user saves becomes the default
            };
            _db.UserApiKeys.Add(existing);
        }

        existing.CipherText = _protector.Protect(key);
        existing.Last4 = key.Length <= 4 ? key : key[^4..];

        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(existing);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SavedApiKey>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var keys = await _db.UserApiKeys
            .Where(k => k.UserId == userId)
            .OrderBy(k => k.Provider)
            .ToListAsync(cancellationToken);
        return [.. keys.Select(ToDto)];
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(Guid userId, string provider, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(provider);
        var key = await _db.UserApiKeys
            .FirstOrDefaultAsync(k => k.UserId == userId && k.Provider == normalized, cancellationToken);
        if (key is null)
        {
            return false;
        }

        _db.UserApiKeys.Remove(key);

        // Promote another provider to default if we removed the current default.
        if (key.IsDefault)
        {
            var next = await _db.UserApiKeys
                .Where(k => k.UserId == userId && k.Provider != normalized)
                .OrderBy(k => k.Provider)
                .FirstOrDefaultAsync(cancellationToken);
            if (next is not null)
            {
                next.IsDefault = true;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> SetDefaultAsync(Guid userId, string provider, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(provider);
        var keys = await _db.UserApiKeys.Where(k => k.UserId == userId).ToListAsync(cancellationToken);
        if (keys.All(k => k.Provider != normalized))
        {
            return false;
        }

        foreach (var key in keys)
        {
            key.IsDefault = key.Provider == normalized;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc/>
    public async Task<string?> GetDecryptedAsync(Guid userId, string provider, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(provider);
        var key = await _db.UserApiKeys
            .FirstOrDefaultAsync(k => k.UserId == userId && k.Provider == normalized, cancellationToken);
        return key is null ? null : _protector.Unprotect(key.CipherText);
    }

    private static string Normalize(string provider)
    {
        var normalized = provider?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!KnownProviders.Contains(normalized))
        {
            throw new ArgumentException(
                $"Unknown provider '{provider}'. Supported: {string.Join(", ", KnownProviders)}.", nameof(provider));
        }
        return normalized;
    }

    private static SavedApiKey ToDto(UserApiKey k) => new(k.Provider, k.Last4, k.IsDefault);
}
