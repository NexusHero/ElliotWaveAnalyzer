namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// One instrument on a user's watchlist (#226): the quick-symbol strip, now user-managed instead
/// of hardcoded. <see cref="LastPrice"/> is a best-effort quote (null when unavailable — never
/// blocks the list). <see cref="HasDraft"/> flags a symbol with an in-progress workspace draft on
/// any interval, so the analyst can see at a glance where they left off.
/// </summary>
public sealed record WatchlistEntry(
    string Symbol,
    int SortOrder,
    decimal? LastPrice,
    bool HasDraft);
