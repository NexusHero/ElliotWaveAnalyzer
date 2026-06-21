using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Infrastructure;

/// <summary>
/// Fetches OHLCV candles for BTC and ETH from the CoinGecko v3 API (free tier).
///
/// Endpoint used: GET /coins/{id}/ohlc?vs_currency=usd&amp;days={days}
/// Returns: [[timestamp_ms, open, high, low, close], ...]
///
/// NOTE: Volume is not provided by the CoinGecko OHLC endpoint on the free tier.
/// Volume is set to 0 in returned candles. This is safe because RSI and MACD
/// only use Close prices. If volume-based indicators are added later, switch to
/// the market_chart endpoint or upgrade to a Pro API key.
///
/// Rate limits (free tier): 10-30 req/min depending on traffic. The daily report
/// workflow calls this once per symbol so we stay well within limits.
/// </summary>
public sealed class CoinGeckoMarketDataProvider(
    HttpClient httpClient,
    ILogger<CoinGeckoMarketDataProvider> logger) : IMarketDataProvider
{
    private static readonly HashSet<string> SupportedSymbols =
        new(StringComparer.OrdinalIgnoreCase) { "BTC", "ETH" };

    /// <inheritdoc/>
    public bool Supports(string symbol) => SupportedSymbols.Contains(symbol);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MarketCandle>> GetCandlesAsync(
        string symbol,
        int days,
        CancellationToken cancellationToken = default)
    {
        var coinId = ToCoinGeckoId(symbol);
        var url = $"coins/{coinId}/ohlc?vs_currency=usd&days={days}";

        logger.LogInformation(
            "Fetching {Days} days of OHLC data for {Symbol} (CoinGecko id: {CoinId})",
            days, symbol, coinId);

        var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        // CoinGecko returns a JSON array of arrays: [[ts_ms, open, high, low, close], ...]
        var raw = await response.Content.ReadFromJsonAsync<List<List<double>>>(
            cancellationToken: cancellationToken);

        if (raw is null || raw.Count == 0)
        {
            logger.LogWarning("CoinGecko returned empty OHLC data for {Symbol}", symbol);
            return [];
        }

        var candles = raw
            .Select(entry => new MarketCandle(
                OpenTime: DateTimeOffset.FromUnixTimeMilliseconds((long)entry[0]).UtcDateTime,
                Open: (decimal)entry[1],
                High: (decimal)entry[2],
                Low: (decimal)entry[3],
                Close: (decimal)entry[4],
                Volume: 0m))  // Not provided by this endpoint; see class doc
            .OrderBy(c => c.OpenTime)
            .ToList();

        logger.LogInformation(
            "Received {Count} candles for {Symbol} ({From:yyyy-MM-dd} – {To:yyyy-MM-dd})",
            candles.Count, symbol, candles.First().OpenTime, candles.Last().OpenTime);

        return candles;
    }

    /// <summary>
    /// Maps ticker symbol to the CoinGecko coin ID used in API paths.
    /// Add new symbols here as needed — no other code changes required (OCP).
    /// </summary>
    private static string ToCoinGeckoId(string symbol) =>
        symbol.ToUpperInvariant() switch
        {
            "BTC" => "bitcoin",
            "ETH" => "ethereum",
            _ => throw new ArgumentOutOfRangeException(
                nameof(symbol), symbol,
                $"CoinGeckoMarketDataProvider does not support symbol '{symbol}'. " +
                $"Supported: {string.Join(", ", SupportedSymbols)}")
        };
}
