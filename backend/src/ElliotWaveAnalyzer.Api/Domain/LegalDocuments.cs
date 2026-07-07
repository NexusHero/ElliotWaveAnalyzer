namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Versioned legal-document metadata (#167 AC4). <see cref="TermsVersion"/>/<see cref="PrivacyVersion"/>
/// are the single source of truth both the served legal pages (<c>LegalEndpoints</c>) and the
/// per-user acceptance record (<c>Infrastructure.Auth.LegalAcceptance</c>, written at signup) read
/// from — bumping a constant here is what "publish a new version" means in this codebase. There is
/// no re-acceptance enforcement for already-registered users yet (an existing account is never asked
/// to re-accept after a version bump); that is a named follow-on, not silently assumed done.
/// </summary>
public static class LegalDocuments
{
    public const string TermsVersion = "1.0";
    public const string TermsEffectiveDate = "2026-07-07";

    public const string PrivacyVersion = "1.0";
    public const string PrivacyEffectiveDate = "2026-07-07";
}
