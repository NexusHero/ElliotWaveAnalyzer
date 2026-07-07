namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// One retained record of a cookie-consent decision (#169 AC5): which non-essential categories were
/// accepted, the policy version they were shown, and when. Consent must be captured from an anonymous
/// visitor too (before login, before there is any account) — <see cref="VisitorId"/> (a client-generated
/// id persisted alongside the decision) is always present; <see cref="UserId"/> is filled in only when
/// the request happens to be authenticated, and is the one place in this schema an owner column is
/// legitimately nullable.
/// </summary>
internal sealed class ConsentRecord
{
    public Guid Id { get; set; }

    /// <summary>Client-generated id for an anonymous visitor; stable across sessions on one device.</summary>
    public string VisitorId { get; set; } = string.Empty;

    /// <summary>Set only when the caller was authenticated at the time consent was recorded.</summary>
    public Guid? UserId { get; set; }

    public bool Analytics { get; set; }

    public bool Marketing { get; set; }

    /// <summary>Version of the consent policy/notice the visitor was shown.</summary>
    public string PolicyVersion { get; set; } = string.Empty;

    public DateTimeOffset RecordedAt { get; set; }
}
