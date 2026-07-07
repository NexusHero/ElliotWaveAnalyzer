using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Abstraction over a social-mood data source (news tone, social volume/polarity, or a computed
/// proxy). Mirrors <see cref="IMarketDataProvider"/> (ISP, OCP): a new vendor adds a class, selected
/// at runtime via <see cref="Supports"/>; nothing existing changes. No concrete implementation ships
/// with the socionomics core slice (#183) — wiring a real vendor is a configuration decision, the
/// same category as a market-data API key.
/// </summary>
public interface ISentimentProvider
{
    /// <summary>Returns true if this provider can supply sentiment for <paramref name="symbol"/>.</summary>
    bool Supports(string symbol);

    /// <summary>
    /// Retrieves raw daily sentiment readings for <paramref name="symbol"/> covering the last
    /// <paramref name="days"/> calendar days. Readings need not already be normalized — the caller
    /// runs them through <see cref="Application.SentimentIndexBuilder"/>. Empty when the provider has
    /// no coverage for the symbol.
    /// </summary>
    Task<IReadOnlyList<SentimentPoint>> GetSentimentAsync(
        string symbol,
        int days,
        CancellationToken cancellationToken = default);
}
