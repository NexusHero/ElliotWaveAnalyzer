namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The deterministic, LLM-free feature description of one Elliott setup — the fingerprint used to
/// find historical analogs. Every field is computed by the engine from a formed count (no model
/// involvement), so two setups that "rhyme" have close feature vectors. The numeric fields are kept
/// in interpretable units here; <see cref="Application.SetupFeatureVector"/> normalises them into the
/// vector the nearest-neighbour search compares.
/// </summary>
/// <param name="Structure">The pattern family (Impulse, Zigzag, …).</param>
/// <param name="Bullish">Direction of the count.</param>
/// <param name="Timeframe">Timeframe label the count was read on (e.g. "1d"), for display/grouping.</param>
/// <param name="Score">Guideline score in [0, 1] (higher = a cleaner textbook count).</param>
/// <param name="ConfluenceStrength">Top confluence-zone strength in [0, 1] (0 = no stacked levels).</param>
/// <param name="RewardToRisk">Reward-to-risk of the primary target (≥ 0; raw ratio, not normalised).</param>
/// <param name="DistanceToInvalidationPct">|entry − invalidation| / entry as a fraction (≥ 0).</param>
/// <param name="RsiRegime">RSI/100 in [0, 1] (0.5 ≈ neutral); the momentum regime at the setup.</param>
/// <param name="MacdRegime">MACD-histogram regime squashed to [0, 1] (0.5 ≈ flat, &gt; 0.5 rising).</param>
public sealed record SetupFeatures(
    StructureKind Structure,
    bool Bullish,
    string Timeframe,
    double Score,
    double ConfluenceStrength,
    double RewardToRisk,
    double DistanceToInvalidationPct,
    double RsiRegime,
    double MacdRegime);
