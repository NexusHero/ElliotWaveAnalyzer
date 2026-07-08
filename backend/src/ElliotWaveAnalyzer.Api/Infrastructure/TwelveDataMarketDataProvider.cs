using System.Globalization;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Infrastructure;

/// <summary>
/// Fetches OHLCV candles from the Twelve Data time-series API (<c>/time_series</c>) for both
/// equities/indices/ETFs and crypto pairs — a single keyed provider replacing the earlier
/// Yahoo Finance (daily+hourly, keyless) / CoinGecko (daily crypto only, keyless) split (#170,
/// ADR-077). Friendly aliases (<c>NASDAQ</c> → <c>IXIC</c>, <c>SP500</c> → <c>SPX</c>, <c>BTC</c> →
/// <c>BTC/USD</c>, <c>ETH</c> → <c>ETH/USD</c>) are mapped; any other symbol passes through, so a
/// ticker resolved by <see cref="ISymbolResolver"/> can be charted directly.
///
/// <para>
/// Requires a configured API key (<c>MarketData:TwelveData:ApiKey</c>) — <see cref="Supports"/> and
/// <see cref="SupportsIntraday"/> report <c>false</c> without one, so
/// <c>TechnicalAnalysisService</c> fails with its existing, honest "no provider supports X" message
/// rather than this provider attempting an unauthenticated call (#170 AC2/AC4).
/// </para>
///
/// <para>
/// Twelve Data returns OHLCV fields as JSON <b>strings</b> (e.g. <c>"open": "150.25"</c>), not raw
/// numbers, and rows newest-first — parsed explicitly via <see cref="decimal.TryParse(string?, NumberStyles, IFormatProvider?, out decimal)"/>
/// and re-sorted ascending, mirroring how <c>YahooFinanceMarketDataProvider</c> skips a trading-gap
/// null and <c>CoinGeckoMarketDataProvider</c> skips a short row: a genuinely malformed row is
/// skipped, never crashes the whole fetch.
/// </para>
///
/// <para>
/// A JSON-level <c>"status": "error"</c> response distinguishes an availability problem
/// (rate-limited/unauthenticated/upstream fault — codes 401/403/429/5xx) from a request-level
/// problem (e.g. an unrecognized symbol, code 400): the former re-throws as
/// <see cref="HttpRequestException"/> so it reaches the same "market data provider is currently
/// unavailable — try again later" 502 path <c>MarketDataEndpoints</c> already gives a transport-level
/// failure (#170 AC2); the latter logs and returns an empty result — the same honest "no data" shape
/// every other provider already uses for an unrecognized symbol.
/// </para>
///
/// <para>
/// Unlike Yahoo's hourly provider, this class does not pre-check a lookback-window limit before
/// fetching: Twelve Data's actual per-plan history depth is unknown until a real subscription is
/// chosen, so a request past the plan's own depth is left to the upstream's own error response
/// (handled above) rather than guessing a number that might be wrong in either direction.
/// </para>
/// </summary>
internal sealed class TwelveDataMarketDataProvider(
    HttpClient httpClient,
    string? apiKey,
    ILogger<TwelveDataMarketDataProvider> logger) : IMarketDataProvider, IIntradayMarketDataProvider
{
    // Friendly public alias → Twelve Data symbol. Unmapped symbols pass straight through (OCP).
    private static readonly IReadOnlyDictionary<string, string> AliasMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["NASDAQ"] = "IXIC",
            ["SP500"] = "SPX",
            ["BTC"] = "BTC/USD",
            ["ETH"] = "ETH/USD",
        };

    // Twelve Data status codes that mean "the provider itself is unavailable right now" (rate
    // limit, auth failure, upstream fault) rather than "this specific request was invalid".
    private static readonly HashSet<int> AvailabilityErrorCodes = [401, 403, 429, 500, 502, 503, 504];

    private bool HasApiKey => !string.IsNullOrWhiteSpace(apiKey);

    /// <inheritdoc/>
    public bool Supports(string symbol) => HasApiKey && !string.IsNullOrWhiteSpace(symbol);

    /// <inheritdoc/>
    public bool SupportsIntraday(string symbol) => HasApiKey && !string.IsNullOrWhiteSpace(symbol);

    /// <inheritdoc/>
    public Task<IReadOnlyList<MarketCandle>> GetCandlesAsync(
        string symbol, int days, CancellationToken cancellationToken = default)
        => FetchAsync(symbol, "1day", Math.Max(days, 1), cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<MarketCandle>> GetHourlyCandlesAsync(
        string symbol, int days, CancellationToken cancellationToken = default)
        => FetchAsync(symbol, "1h", Math.Max(days, 1), cancellationToken);

    private async Task<IReadOnlyList<MarketCandle>> FetchAsync(
        string symbol, string interval, int days, CancellationToken cancellationToken)
    {
        var mapped = AliasMap.TryGetValue(symbol, out var alias) ? alias : symbol;

        var end = DateTimeOffset.UtcNow;
        var start = end.AddDays(-days);
        var url =
            $"time_series?symbol={Uri.EscapeDataString(mapped)}&interval={interval}" +
            $"&start_date={start:yyyy-MM-dd}&end_date={end:yyyy-MM-dd}&timezone=UTC&format=JSON" +
            $"&apikey={Uri.EscapeDataString(apiKey ?? string.Empty)}";

        logger.LogInformation(
            "Fetching {Days} days of {Interval} candles for {Symbol} (Twelve Data symbol: {MappedSymbol})",
            days, interval, symbol, mapped);

        var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TwelveDataResponse>(cancellationToken);

        if (string.Equals(payload?.Status, "error", StringComparison.OrdinalIgnoreCase))
        {
            if (payload?.Code is { } code && AvailabilityErrorCodes.Contains(code))
            {
                throw new HttpRequestException(
                    $"Twelve Data reported an availability error ({code}) for {symbol}: {payload.Message}");
            }

            logger.LogWarning(
                "Twelve Data rejected the request for {Symbol} ({Code}): {Message}",
                symbol, payload?.Code, payload?.Message);
            return [];
        }

        if (payload?.Values is null || payload.Values.Length == 0)
        {
            logger.LogWarning("Twelve Data returned no usable candles for {Symbol}", symbol);
            return [];
        }

        var candles = new List<MarketCandle>(payload.Values.Length);
        foreach (var value in payload.Values)
        {
            if (TryParseCandle(value, out var candle))
            {
                candles.Add(candle);
            }
        }

        candles.Sort((a, b) => a.OpenTime.CompareTo(b.OpenTime));

        logger.LogInformation(
            "Received {Count} {Interval} candles for {Symbol}", candles.Count, interval, symbol);
        return candles;
    }

    private static bool TryParseCandle(TwelveDataValue value, out MarketCandle candle)
    {
        candle = null!;

        if (string.IsNullOrWhiteSpace(value.Datetime) ||
            !DateTime.TryParse(
                value.Datetime, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var openTime) ||
            !TryParseDecimal(value.Open, out var open) ||
            !TryParseDecimal(value.High, out var high) ||
            !TryParseDecimal(value.Low, out var low) ||
            !TryParseDecimal(value.Close, out var close))
        {
            return false;
        }

        _ = TryParseDecimal(value.Volume, out var volume); // optional; defaults to 0 when absent

        candle = new MarketCandle(DateTime.SpecifyKind(openTime, DateTimeKind.Utc), open, high, low, close, volume);
        return true;
    }

    private static bool TryParseDecimal(string? raw, out decimal value) =>
        decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out value);

    // ─── Response DTOs ────────────────────────────────────────────────────────

    private sealed class TwelveDataResponse
    {
        public string? Status { get; init; }
        public string? Message { get; init; }
        public int? Code { get; init; }
        public TwelveDataValue[]? Values { get; init; }
    }

    private sealed class TwelveDataValue
    {
        public string? Datetime { get; init; }
        public string? Open { get; init; }
        public string? High { get; init; }
        public string? Low { get; init; }
        public string? Close { get; init; }
        public string? Volume { get; init; }
    }
}
