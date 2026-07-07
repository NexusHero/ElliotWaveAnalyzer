namespace ElliotWaveAnalyzer.Api.Domain.Account;

/// <summary>
/// All of one user's personal data, in one portable payload (DSGVO Art. 20, #168 AC1). Composed
/// entirely from data the user themselves created or that describes their own account — never
/// another user's data, never a secret that must not leave the server (a stored key's ciphertext,
/// a session token/hash) (#168 AC5).
/// </summary>
public sealed record AccountExport(
    AccountExportProfile Profile,
    IReadOnlyList<AccountExportAnalysis> Analyses,
    IReadOnlyList<AccountExportApiKey> ApiKeys,
    IReadOnlyList<AccountExportDepot> Depots,
    IReadOnlyList<AccountExportLlmUsagePeriod> LlmUsage);
