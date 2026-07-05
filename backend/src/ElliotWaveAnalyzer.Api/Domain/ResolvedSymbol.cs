namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// One instrument a free-text query (ticker, company name or ISIN) resolved to. The
/// <see cref="Symbol"/> is the data-source ticker the market-data providers understand
/// (e.g. "RKLB", "^IXIC", "SI=F") — it is what the client sends to the analysis endpoints.
/// </summary>
/// <param name="Symbol">Data-source ticker (what market-data endpoints accept).</param>
/// <param name="Name">Human-readable instrument name.</param>
/// <param name="AssetClass">Source classification, e.g. EQUITY, ETF, INDEX, FUTURE, CRYPTOCURRENCY.</param>
/// <param name="Exchange">Display name of the listing exchange, if the source provides one.</param>
public sealed record ResolvedSymbol(
    string Symbol,
    string Name,
    string AssetClass,
    string? Exchange);
