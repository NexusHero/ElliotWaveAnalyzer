namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>Request body to add a symbol to the caller's watchlist (#226).</summary>
public sealed record AddWatchlistEntryRequest(string Symbol);
