using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Orchestrates provider selection → data fetch → indicator calculation.
///
/// Provider selection follows the Chain-of-Responsibility pattern: for daily/weekly the service
/// uses the first <see cref="IMarketDataProvider"/> that <see cref="IMarketDataProvider.Supports"/>
/// the symbol; for 1H/4H it uses the first <see cref="IIntradayMarketDataProvider"/> that
/// <see cref="IIntradayMarketDataProvider.SupportsIntraday"/> it (4H is resampled from hourly).
/// Adding a data source is one DI registration — this class never changes (OCP). If no source can
/// serve the requested timeframe the caller gets an <see cref="ArgumentException"/> (honest failure,
/// not a silent fallback to a different timeframe).
/// </summary>
public sealed class TechnicalAnalysisService(
    IEnumerable<IMarketDataProvider> providers,
    IEnumerable<IIntradayMarketDataProvider> intradayProviders,
    IIndicatorCalculator calculator,
    ILogger<TechnicalAnalysisService> logger) : ITechnicalAnalysisService
{
    private readonly IReadOnlyList<IMarketDataProvider> _providers = [.. providers];
    private readonly IReadOnlyList<IIntradayMarketDataProvider> _intradayProviders = [.. intradayProviders];

    /// <inheritdoc/>
    public async Task<TechnicalAnalysisResult> GetAnalysisAsync(
        string symbol,
        int days = 90,
        CandleInterval interval = CandleInterval.OneDay,
        CancellationToken cancellationToken = default)
    {
        var candles = interval is CandleInterval.OneHour or CandleInterval.FourHours
            ? await GetIntradayAsync(symbol, days, interval, cancellationToken)
            : await GetDailyAsync(symbol, days, interval, cancellationToken);

        // Run RSI and MACD on the presented (resampled) series — the calculator is stateless.
        var rsi = calculator.CalculateRsi(candles);
        var macd = calculator.CalculateMacd(candles);

        return new TechnicalAnalysisResult(symbol, candles, macd, rsi);
    }

    private async Task<IReadOnlyList<MarketCandle>> GetDailyAsync(
        string symbol, int days, CandleInterval interval, CancellationToken cancellationToken)
    {
        var provider = _providers.FirstOrDefault(p => p.Supports(symbol))
            ?? throw new ArgumentException(
                $"No market data provider supports symbol '{symbol}'. " +
                $"Registered providers support: {DescribeProviders()}",
                nameof(symbol));

        logger.LogInformation(
            "Analysing {Symbol} ({Days} days, {Interval}) using {Provider}",
            symbol, days, interval, provider.GetType().Name);

        var daily = await provider.GetCandlesAsync(symbol, days, cancellationToken);
        return CandleResampler.Resample(daily, interval);
    }

    private async Task<IReadOnlyList<MarketCandle>> GetIntradayAsync(
        string symbol, int days, CandleInterval interval, CancellationToken cancellationToken)
    {
        var provider = _intradayProviders.FirstOrDefault(p => p.SupportsIntraday(symbol))
            ?? throw new ArgumentException(
                $"No intraday data source can serve '{symbol}' for {interval} analysis. " +
                "Use a daily or weekly timeframe for this instrument.",
                nameof(symbol));

        logger.LogInformation(
            "Analysing {Symbol} ({Days} days, {Interval}, intraday) using {Provider}",
            symbol, days, interval, provider.GetType().Name);

        var hourly = await provider.GetHourlyCandlesAsync(symbol, days, cancellationToken);
        return CandleResampler.Resample(hourly, interval); // OneHour: pass-through; FourHours: 4H buckets
    }

    private string DescribeProviders() =>
        _providers.Count == 0
            ? "(none)"
            : string.Join(", ", _providers.Select(p => p.GetType().Name));
}
