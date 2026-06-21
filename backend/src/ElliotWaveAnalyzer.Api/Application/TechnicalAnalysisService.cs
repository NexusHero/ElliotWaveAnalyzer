using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Orchestrates provider selection → data fetch → indicator calculation.
///
/// Provider selection follows the Chain-of-Responsibility pattern:
/// the service iterates the injected <see cref="IMarketDataProvider"/> list
/// and uses the first one that claims to <see cref="IMarketDataProvider.Supports"/>
/// the requested symbol. Adding a new data source (e.g. Yahoo Finance for NASDAQ)
/// only requires registering another <see cref="IMarketDataProvider"/> in DI —
/// this class never changes (OCP).
/// </summary>
public sealed class TechnicalAnalysisService(
    IEnumerable<IMarketDataProvider> providers,
    IIndicatorCalculator calculator,
    ILogger<TechnicalAnalysisService> logger) : ITechnicalAnalysisService
{
    private readonly IReadOnlyList<IMarketDataProvider> _providers = [.. providers];

    /// <inheritdoc/>
    public async Task<TechnicalAnalysisResult> GetAnalysisAsync(
        string symbol,
        int days = 90,
        CancellationToken cancellationToken = default)
    {
        var provider = _providers.FirstOrDefault(p => p.Supports(symbol))
            ?? throw new ArgumentException(
                $"No market data provider supports symbol '{symbol}'. " +
                $"Registered providers support: {DescribeProviders()}",
                nameof(symbol));

        logger.LogInformation(
            "Analysing {Symbol} ({Days} days) using {Provider}",
            symbol, days, provider.GetType().Name);

        var candles = await provider.GetCandlesAsync(symbol, days, cancellationToken);

        // Run RSI and MACD on the same candle set — the calculator is stateless.
        var rsi = calculator.CalculateRsi(candles);
        var macd = calculator.CalculateMacd(candles);

        return new TechnicalAnalysisResult(symbol, candles, macd, rsi);
    }

    private string DescribeProviders() =>
        _providers.Count == 0
            ? "(none)"
            : string.Join(", ", _providers.Select(p => p.GetType().Name));
}
