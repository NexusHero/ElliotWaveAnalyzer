using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Per-user vault for LLM provider API keys. Keys are encrypted at rest; the plaintext is only
/// ever seen on the way in (to save) and on the way out to the provider (via
/// <see cref="GetDecryptedAsync"/>) — never returned to the client. Every operation is scoped
/// to the calling user.
/// </summary>
public interface IUserKeyStore
{
    /// <summary>
    /// Encrypts and stores (or replaces) the key for <paramref name="provider"/>. The first key a
    /// user saves becomes their default. Returns the safe metadata view.
    /// </summary>
    Task<SavedApiKey> SaveAsync(Guid userId, string provider, string plaintextKey, CancellationToken cancellationToken = default);

    /// <summary>Lists the user's configured providers (metadata only — never the key).</summary>
    Task<IReadOnlyList<SavedApiKey>> ListAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the key for <paramref name="provider"/>. If it was the default, another key (if any)
    /// is promoted. Returns false when the user had no key for that provider.
    /// </summary>
    Task<bool> DeleteAsync(Guid userId, string provider, CancellationToken cancellationToken = default);

    /// <summary>Makes <paramref name="provider"/> the user's default. False when it isn't configured.</summary>
    Task<bool> SetDefaultAsync(Guid userId, string provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the decrypted key for <paramref name="provider"/>, or null when the user has none.
    /// For server-side use when calling the provider — never exposed through an endpoint.
    /// </summary>
    Task<string?> GetDecryptedAsync(Guid userId, string provider, CancellationToken cancellationToken = default);
}
