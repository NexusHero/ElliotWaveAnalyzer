namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// Persisted workspace draft (#226): one row per (user, symbol, interval), overwritten in place on
/// every auto-save. Annotations and settings are stored as JSON — both mirror plain frontend view
/// state, not domain geometry any other backend logic needs to interpret.
/// </summary>
internal sealed class WorkspaceDraftRow
{
    public Guid Id { get; set; }

    /// <summary>Owner. Every query is scoped by this — no cross-user access.</summary>
    public Guid UserId { get; set; }

    public string Symbol { get; set; } = string.Empty;
    public string Interval { get; set; } = string.Empty;
    public string AnnotationsJson { get; set; } = "[]";
    public string SettingsJson { get; set; } = "{}";
    public DateTimeOffset UpdatedAt { get; set; }
}
