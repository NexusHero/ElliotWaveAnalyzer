namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// A user's LLM provider API key, encrypted at rest. Only the ciphertext and the last four
/// characters are stored — the plaintext is never persisted and never returned to the client.
/// One row per (user, provider); one provider per user is marked the default.
/// </summary>
internal sealed class UserApiKey
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Provider key: "gemini" / "claude" / "openai".</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Data-Protection-encrypted key (base64). Never the plaintext.</summary>
    public string CipherText { get; set; } = string.Empty;

    /// <summary>Last four characters of the plaintext, for display only.</summary>
    public string Last4 { get; set; } = string.Empty;

    /// <summary>Whether this is the user's active provider.</summary>
    public bool IsDefault { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
