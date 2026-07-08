using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Abstraction over a market data source (Twelve Data, etc.).
/// <para>
/// ISP: deliberately narrow — one responsibility per method.
/// OCP: new data sources add a class; nothing existing changes.
/// Multiple implementations are registered in DI and selected at runtime
/// via <see cref="Supports"/>.
/// </para>
/// </summary>
public interface IMarketDataProvider
{
    /// <summary>Returns true if this provider can supply data for <paramref name="symbol"/>.</summary>
    bool Supports(string symbol);

    /// <summary>
    /// Retrieves OHLCV candles for <paramref name="symbol"/> covering the last
    /// <paramref name="days"/> calendar days, ordered ascending by date.
    /// </summary>
    Task<IReadOnlyList<MarketCandle>> GetCandlesAsync(
        string symbol,
        int days,
        CancellationToken cancellationToken = default);
}
