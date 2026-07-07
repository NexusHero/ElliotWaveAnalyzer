using ElliotWaveAnalyzer.Api.Domain.Account;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// The DSGVO/GDPR self-service data rights (#168): export a user's own personal data (Art. 20
/// portability) and irreversibly delete their account and every associated row (Art. 17 erasure).
/// </summary>
public interface IAccountRightsService
{
    Task<AccountExport> ExportDataAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the account. When the account has a password, <paramref name="currentPassword"/> must
    /// match it — an explicit re-confirmation for an irreversible action. An OAuth-only account (no
    /// password ever set) has nothing to re-confirm with, so the authenticated session alone gates it.
    /// </summary>
    Task<AccountDeletionResult> DeleteAccountAsync(
        Guid userId, string? currentPassword, string? requestedByIp, CancellationToken cancellationToken = default);
}
