namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Resolves the current market price for a data-source symbol (#114). Kept narrow (ISP): a single
/// "what's the latest price" lookup, independent of the richer candle/indicator retrieval
/// <see cref="ITechnicalAnalysisService"/> already provides. Never throws for an unavailable quote —
/// returns null so a caller degrades gracefully (leaves the field null) rather than crashing.
/// </summary>
public interface IQuoteProvider
{
    /// <summary>The latest known price for <paramref name="symbol"/>, or null when unavailable.</summary>
    Task<decimal?> GetLatestPriceAsync(string symbol, CancellationToken cancellationToken = default);
}
