namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// A record that an account was deleted (#168 AC4) — who (by id, never email/name) and when. No
/// personal data is retained: the deleted user's id remains meaningful only as an opaque value once
/// the account itself is gone, and every other column is metadata about the deletion event, not the
/// deleted person. Written inside the same transaction as the deletion itself.
/// </summary>
internal sealed class AccountDeletionAudit
{
    public Guid Id { get; set; }

    /// <summary>Id of the deleted account. Not a foreign key — it must outlive the row it names.</summary>
    public Guid DeletedUserId { get; set; }

    public DateTimeOffset DeletedAt { get; set; }

    public string? RequestedByIp { get; set; }
}
