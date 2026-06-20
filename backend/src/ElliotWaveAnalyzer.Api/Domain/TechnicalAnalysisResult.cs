namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Aggregated result for a single symbol: raw candles plus pre-calculated indicators.
/// Designed as a single cohesive response to minimize round-trips from the frontend.
/// </summary>
public sealed record TechnicalAnalysisResult(
    string Symbol,
    IReadOnlyList<MarketCandle> Candles,
    IReadOnlyList<MacdResult> Macd,
    IReadOnlyList<RsiResult> Rsi);
