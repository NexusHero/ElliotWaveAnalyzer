namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// Records that a user accepted the Terms of Service and Privacy Policy at signup (#167 AC4),
/// and which version of each — never grown onto <see cref="AppUser"/> directly, following the
/// established per-user-table + cascade-on-delete pattern (#168 AC3, mirrored by
/// <see cref="ConsentRecord"/> in #169).
/// </summary>
internal sealed class LegalAcceptance
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string TermsVersion { get; set; } = string.Empty;

    public string PrivacyVersion { get; set; } = string.Empty;

    public DateTimeOffset AcceptedAt { get; set; }
}
