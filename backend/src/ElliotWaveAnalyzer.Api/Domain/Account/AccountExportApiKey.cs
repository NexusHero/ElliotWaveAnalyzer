namespace ElliotWaveAnalyzer.Api.Domain.Account;

/// <summary>
/// Metadata about a stored provider key — never the ciphertext, never a decrypted value (#168 AC5).
/// </summary>
public sealed record AccountExportApiKey(string Provider, string Last4, bool IsDefault, DateTimeOffset CreatedAt);
