using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Fetches the symbol's candles and hands them, with the analyst's edited annotations, to the pure
/// <see cref="WaveVerifier"/> (REQ-031). The only I/O is the candle fetch; all judgment is deterministic
/// and lives in the verifier. No LLM call, so this is cheap enough to run on every debounced edit.
/// </summary>
public sealed class WaveVerificationService(
    IEnumerable<IMarketDataProvider> marketDataProviders) : IWaveVerificationService
{
    private readonly IReadOnlyList<IMarketDataProvider> _marketDataProviders = [.. marketDataProviders];

    /// <inheritdoc/>
    public async Task<WaveVerification> VerifyAsync(
        string symbol,
        IReadOnlyList<WaveAnnotation> annotations,
        int lookbackDays,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotations);

        var provider = _marketDataProviders.FirstOrDefault(p => p.Supports(symbol))
            ?? throw new ArgumentException(
                $"No market data provider supports symbol '{symbol}'.", nameof(symbol));

        var candles = await provider.GetCandlesAsync(symbol, lookbackDays, cancellationToken);

        return WaveVerifier.Verify(annotations, candles);
    }
}
