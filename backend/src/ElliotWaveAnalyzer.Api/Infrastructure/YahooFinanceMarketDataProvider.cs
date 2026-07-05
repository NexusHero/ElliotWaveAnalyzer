using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Infrastructure;

/// <summary>
/// Fetches OHLCV candles from the Yahoo Finance chart API (<c>/v8/finance/chart/{symbol}</c>) for
/// arbitrary instruments — equities, ETFs, indices and metals/commodities (via Yahoo tickers such
/// as <c>SI=F</c>). Friendly aliases (e.g. <c>NASDAQ</c> → <c>^IXIC</c>) are mapped; any other
/// symbol is passed through to Yahoo as-is, so a ticker resolved by <see cref="ISymbolResolver"/>
/// can be charted directly.
///
/// It is the <b>fallback</b> daily provider (<see cref="Supports"/> returns true for everything, so
/// it must be registered last — earlier providers like CoinGecko claim their symbols first) and it
/// serves <b>hourly</b> candles for 1H/4H analysis (<see cref="IIntradayMarketDataProvider"/>).
/// Yahoo's hourly history reaches ~2 years back; requests beyond that raise
/// <see cref="MarketDataRangeException"/> rather than silently returning a shorter range.
///
/// NOTE: Yahoo's public chart endpoint needs no API key. Prices are read straight to decimal —
/// never through double — to preserve precision for financial data.
/// </summary>
internal sealed class YahooFinanceMarketDataProvider(
    HttpClient httpClient,
    ILogger<YahooFinanceMarketDataProvider> logger) : IMarketDataProvider, IIntradayMarketDataProvider
{
    /// <summary>Yahoo's hourly (60m) history depth. Beyond this the chart API returns no data.</summary>
    internal const int MaxHourlyLookbackDays = 730;

    // Friendly public alias → Yahoo symbol. Unmapped symbols pass straight through (OCP).
    private static readonly IReadOnlyDictionary<string, string> AliasMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["NASDAQ"] = "^IXIC",  // NASDAQ Composite
            ["SP500"] = "^GSPC",   // S&P 500
            ["SPX"] = "^GSPC",
        };

    /// <inheritdoc/>
    public bool Supports(string symbol) => !string.IsNullOrWhiteSpace(symbol);

    /// <inheritdoc/>
    public bool SupportsIntraday(string symbol) => !string.IsNullOrWhiteSpace(symbol);

    /// <inheritdoc/>
    public Task<IReadOnlyList<MarketCandle>> GetCandlesAsync(
        string symbol, int days, CancellationToken cancellationToken = default)
        => FetchAsync(symbol, "1d", Math.Max(days, 1), cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<MarketCandle>> GetHourlyCandlesAsync(
        string symbol, int days, CancellationToken cancellationToken = default)
    {
        var window = Math.Max(days, 1);
        if (window > MaxHourlyLookbackDays)
        {
            throw new MarketDataRangeException(
                $"Yahoo Finance serves at most {MaxHourlyLookbackDays} days of hourly candles " +
                $"(requested {window}). Use a coarser timeframe or a shorter range.",
                MaxHourlyLookbackDays);
        }

        return FetchAsync(symbol, "60m", window, cancellationToken);
    }

    private async Task<IReadOnlyList<MarketCandle>> FetchAsync(
        string symbol, string interval, int days, CancellationToken cancellationToken)
    {
        var yahooSymbol = AliasMap.TryGetValue(symbol, out var mapped) ? mapped : symbol;

        var end = DateTimeOffset.UtcNow;
        var start = end.AddDays(-days);
        var url =
            $"v8/finance/chart/{Uri.EscapeDataString(yahooSymbol)}" +
            $"?interval={interval}&period1={start.ToUnixTimeSeconds()}&period2={end.ToUnixTimeSeconds()}";

        logger.LogInformation(
            "Fetching {Days} days of {Interval} candles for {Symbol} (Yahoo symbol: {YahooSymbol})",
            days, interval, symbol, yahooSymbol);

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

        logger.LogInformation("Received {Count} {Interval} candles for {Symbol}", candles.Count, interval, symbol);
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
