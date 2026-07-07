using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Infrastructure;

/// <summary>
/// Derives "the latest price" from the existing candle pipeline (#114) — the last daily close from
/// <see cref="ITechnicalAnalysisService"/>, which already owns provider selection. A short lookback
/// is enough; only the most recent candle is used. Degrades to null (never throws) when no provider
/// supports the symbol or the requested range is out of bounds — the caller (
/// <see cref="Application.DepotEnrichmentService"/>) leaves the field unset rather than failing.
/// </summary>
internal sealed class TechnicalAnalysisQuoteProvider(ITechnicalAnalysisService technicalAnalysis) : IQuoteProvider
{
    private const int LookbackDays = 30;

    public async Task<decimal?> GetLatestPriceAsync(string symbol, CancellationToken cancellationToken = default)
    {
        try
        {
            var analysis = await technicalAnalysis.GetAnalysisAsync(
                symbol, LookbackDays, CandleInterval.OneDay, cancellationToken);
            return analysis.Candles.Count > 0 ? analysis.Candles[^1].Close : null;
        }
        catch (Exception ex) when (ex is ArgumentException or MarketDataRangeException)
        {
            return null;
        }
    }
}
