using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Fetches the symbol's candles and hands them, with the analyst's edited annotations, to the pure
/// <see cref="WaveVerifier"/> (REQ-031). The only I/O is the candle fetch; all judgment is deterministic
/// and lives in the verifier. No LLM call, so this is cheap enough to run on every debounced edit.
/// </summary>
public sealed class WaveVerificationService(
    IEnumerable<IMarketDataProvider> marketDataProviders,
    IEnumerable<IIntradayMarketDataProvider> intradayProviders) : IWaveVerificationService
{
    private readonly IReadOnlyList<IMarketDataProvider> _marketDataProviders = [.. marketDataProviders];
    private readonly IReadOnlyList<IIntradayMarketDataProvider> _intradayProviders = [.. intradayProviders];

    /// <inheritdoc/>
    public async Task<WaveVerification> VerifyAsync(
        string symbol,
        IReadOnlyList<WaveAnnotation> annotations,
        int lookbackDays,
        CandleInterval interval = CandleInterval.OneDay,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotations);

        // Verify against the SAME series the chart displayed (interval-resampled). Snapping
        // weekly-placed pivots against raw daily candles would wrongly reject them: a weekly
        // bar's extreme prints on a different calendar day than the bar's own date.
        var raw = interval is CandleInterval.OneHour or CandleInterval.FourHours
            ? await GetIntradayAsync(symbol, lookbackDays, cancellationToken)
            : await GetDailyAsync(symbol, lookbackDays, cancellationToken);
        var candles = CandleResampler.Resample(raw, interval);

        return WaveVerifier.Verify(annotations, candles);
    }

    private async Task<IReadOnlyList<MarketCandle>> GetDailyAsync(
        string symbol, int lookbackDays, CancellationToken cancellationToken)
    {
        var provider = _marketDataProviders.FirstOrDefault(p => p.Supports(symbol))
            ?? throw new ArgumentException(
                $"No market data provider supports symbol '{symbol}'.", nameof(symbol));
        return await provider.GetCandlesAsync(symbol, lookbackDays, cancellationToken);
    }

    private async Task<IReadOnlyList<MarketCandle>> GetIntradayAsync(
        string symbol, int lookbackDays, CancellationToken cancellationToken)
    {
        var provider = _intradayProviders.FirstOrDefault(p => p.SupportsIntraday(symbol))
            ?? throw new ArgumentException(
                $"No intraday data source can serve '{symbol}' for intraday verification. " +
                "Use a daily or weekly timeframe for this instrument.", nameof(symbol));
        return await provider.GetHourlyCandlesAsync(symbol, lookbackDays, cancellationToken);
    }
}
