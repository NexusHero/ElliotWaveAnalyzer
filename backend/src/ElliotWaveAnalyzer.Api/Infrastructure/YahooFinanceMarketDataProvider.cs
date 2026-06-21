using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Infrastructure;

/// <summary>
/// Fetches daily OHLCV candles for stock indices from the Yahoo Finance chart API
/// (<c>/v8/finance/chart/{symbol}</c>). Adds equity-market coverage (NASDAQ, S&amp;P 500)
/// alongside the crypto provider.
///
/// Registered as another <see cref="IMarketDataProvider"/>; selection happens via
/// <see cref="Supports"/>, so adding this required no change to existing providers or
/// to the orchestrating services (OCP).
///
/// NOTE: this uses Yahoo's public chart endpoint (no API key). Prices are read straight
/// to decimal — never through double — to preserve precision for financial data.
/// </summary>
public sealed class YahooFinanceMarketDataProvider(
    HttpClient httpClient,
    ILogger<YahooFinanceMarketDataProvider> logger) : IMarketDataProvider
{
    // Public ticker → Yahoo index symbol. Extend here to add more indices (OCP).
    private static readonly IReadOnlyDictionary<string, string> SymbolMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["NASDAQ"] = "^IXIC",  // NASDAQ Composite
            ["SP500"] = "^GSPC",   // S&P 500
            ["SPX"] = "^GSPC",
        };

    /// <inheritdoc/>
    public bool Supports(string symbol) => SymbolMap.ContainsKey(symbol);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MarketCandle>> GetCandlesAsync(
        string symbol,
        int days,
        CancellationToken cancellationToken = default)
    {
        if (!SymbolMap.TryGetValue(symbol, out var yahooSymbol))
        {
            throw new ArgumentOutOfRangeException(
                nameof(symbol), symbol,
                $"YahooFinanceMarketDataProvider does not support symbol '{symbol}'. " +
                $"Supported: {string.Join(", ", SymbolMap.Keys)}");
        }

        var end = DateTimeOffset.UtcNow;
        var start = end.AddDays(-Math.Max(days, 1));
        var url =
            $"v8/finance/chart/{Uri.EscapeDataString(yahooSymbol)}" +
            $"?interval=1d&period1={start.ToUnixTimeSeconds()}&period2={end.ToUnixTimeSeconds()}";

        logger.LogInformation(
            "Fetching {Days} days of daily candles for {Symbol} (Yahoo symbol: {YahooSymbol})",
            days, symbol, yahooSymbol);

        var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<YahooResponse>(cancellationToken);

        var result = payload?.Chart?.Result?.FirstOrDefault();
        var timestamps = result?.Timestamp;
        var quote = result?.Indicators?.Quote?.FirstOrDefault();

        if (timestamps is null || quote?.Close is null || timestamps.Length == 0)
        {
            logger.LogWarning(
                "Yahoo Finance returned no usable candles for {Symbol}: {Error}",
                symbol, payload?.Chart?.Error?.Description ?? "empty result");
            return [];
        }

        var candles = new List<MarketCandle>(timestamps.Length);
        for (var i = 0; i < timestamps.Length; i++)
        {
            // Yahoo returns null entries for non-trading gaps — skip incomplete rows.
            if (Get(quote.Open, i) is not { } open ||
                Get(quote.High, i) is not { } high ||
                Get(quote.Low, i) is not { } low ||
                Get(quote.Close, i) is not { } close)
            {
                continue;
            }

            candles.Add(new MarketCandle(
                OpenTime: DateTimeOffset.FromUnixTimeSeconds(timestamps[i]).UtcDateTime,
                Open: open,
                High: high,
                Low: low,
                Close: close,
                Volume: Get(quote.Volume, i) ?? 0m));
        }

        candles.Sort((a, b) => a.OpenTime.CompareTo(b.OpenTime));

        logger.LogInformation("Received {Count} candles for {Symbol}", candles.Count, symbol);
        return candles;
    }

    private static decimal? Get(decimal?[]? array, int index) =>
        array is not null && index < array.Length ? array[index] : null;

    // ─── Response DTOs ────────────────────────────────────────────────────────

    private sealed class YahooResponse
    {
        public YahooChart? Chart { get; init; }
    }

    private sealed class YahooChart
    {
        public YahooResult[]? Result { get; init; }
        public YahooError? Error { get; init; }
    }

    private sealed class YahooError
    {
        public string? Code { get; init; }
        public string? Description { get; init; }
    }

    private sealed class YahooResult
    {
        public long[]? Timestamp { get; init; }
        public YahooIndicators? Indicators { get; init; }
    }

    private sealed class YahooIndicators
    {
        public YahooQuote[]? Quote { get; init; }
    }

    private sealed class YahooQuote
    {
        public decimal?[]? Open { get; init; }
        public decimal?[]? High { get; init; }
        public decimal?[]? Low { get; init; }
        public decimal?[]? Close { get; init; }
        public decimal?[]? Volume { get; init; }
    }
}
