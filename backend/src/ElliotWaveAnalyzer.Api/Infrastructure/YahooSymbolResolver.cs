using System.Text.Json.Serialization;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Infrastructure;

/// <summary>
/// Resolves tickers, company names and <b>ISINs</b> via the Yahoo Finance search endpoint
/// (<c>/v1/finance/search</c>) — the same host that serves candles, no API key. Yahoo's search
/// accepts an ISIN and returns the corresponding ticker, so depot positions resolve directly
/// without a separate ISIN registry (OpenFIGI remains a documented upgrade path — ADR-022).
/// Best match first; empty when nothing matches.
/// </summary>
internal sealed class YahooSymbolResolver(
    HttpClient httpClient,
    ILogger<YahooSymbolResolver> logger) : ISymbolResolver
{
    public async Task<IReadOnlyList<ResolvedSymbol>> SearchAsync(
        string query, CancellationToken cancellationToken = default)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
        {
            return [];
        }

        var url = $"v1/finance/search?q={Uri.EscapeDataString(trimmed)}&quotesCount=10&newsCount=0";
        var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<SearchResponse>(cancellationToken);
        var quotes = payload?.Quotes;
        if (quotes is null || quotes.Length == 0)
        {
            logger.LogInformation("Symbol search for '{Query}' returned no matches", trimmed);
            return [];
        }

        return [.. quotes
            .Where(q => !string.IsNullOrWhiteSpace(q.Symbol))
            .Select(q => new ResolvedSymbol(
                Symbol: q.Symbol!,
                Name: q.LongName ?? q.ShortName ?? q.Symbol!,
                AssetClass: (q.QuoteType ?? "UNKNOWN").ToUpperInvariant(),
                Exchange: q.ExchangeDisplay))];
    }

    // ─── Response DTOs ────────────────────────────────────────────────────────

    private sealed class SearchResponse
    {
        public SearchQuote[]? Quotes { get; init; }
    }

    private sealed class SearchQuote
    {
        public string? Symbol { get; init; }

        [JsonPropertyName("shortname")]
        public string? ShortName { get; init; }

        [JsonPropertyName("longname")]
        public string? LongName { get; init; }

        [JsonPropertyName("quoteType")]
        public string? QuoteType { get; init; }

        [JsonPropertyName("exchDisp")]
        public string? ExchangeDisplay { get; init; }
    }
}
