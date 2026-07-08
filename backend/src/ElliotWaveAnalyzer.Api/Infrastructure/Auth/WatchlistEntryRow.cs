namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>Persisted watchlist entry (#226): one row per (user, symbol).</summary>
internal sealed class WatchlistEntryRow
{
    public Guid Id { get; set; }

    /// <summary>Owner. Every query is scoped by this — no cross-user access.</summary>
    public Guid UserId { get; set; }

    public string Symbol { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
