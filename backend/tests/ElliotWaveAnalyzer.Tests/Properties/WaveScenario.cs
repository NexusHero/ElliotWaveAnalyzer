using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Properties;

/// <summary>
/// A generated test scenario for the property suites: a valid candle series and a set of wave
/// annotations placed on real candle extremes (so they snap). Produced by <see cref="PropertyGenerators"/>.
/// </summary>
internal sealed record WaveScenario(
    IReadOnlyList<MarketCandle> Candles,
    IReadOnlyList<WaveAnnotation> Annotations);
