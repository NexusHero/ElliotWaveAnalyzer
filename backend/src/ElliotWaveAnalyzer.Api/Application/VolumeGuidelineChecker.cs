using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Deterministic guideline (#224): the canonical volume signature — wave 3 (the conviction wave)
/// prints stronger volume than wave 5 in an impulse; wave B (the corrective pause) prints anemic
/// volume against both wave A and wave C. Pure and static, mirroring
/// <see cref="MomentumDivergenceChecker"/>: no DI dependency, callers pass the candle series they
/// already have. A guideline, never a hard rule (AC4).
/// </summary>
public static class VolumeGuidelineChecker
{
    public const string RuleName = "Guideline — volume signature";

    /// <summary>
    /// Checks <paramref name="pivots"/> (origin + labelled waves, chronological) against
    /// <paramref name="candles"/>'s volume. Understands the same two shapes
    /// <see cref="MomentumDivergenceChecker"/> does (6-pivot impulse or 4-pivot ABC correction);
    /// any other shape, or a window where every candle reports zero volume (a provider that
    /// doesn't supply it, e.g. CoinGecko's free OHLC endpoint), yields
    /// <see cref="RuleStatus.Indeterminate"/> — never a guessed fail (AC2).
    /// </summary>
    public static RuleResult Check(IReadOnlyList<WaveAnnotation> pivots, IReadOnlyList<MarketCandle> candles)
    {
        ArgumentNullException.ThrowIfNull(pivots);
        ArgumentNullException.ThrowIfNull(candles);

        var sorted = pivots.OrderBy(p => p.Date).ToList();
        return sorted.Count switch
        {
            >= 6 => CheckImpulse(sorted, candles),
            4 => CheckCorrection(sorted, candles),
            _ => Indeterminate("Needs a 5-wave impulse (through wave 5) or an ABC correction."),
        };
    }

    private static RuleResult CheckImpulse(IReadOnlyList<WaveAnnotation> p, IReadOnlyList<MarketCandle> candles)
    {
        var wave3 = VolumeBetween(candles, p[2].Date, p[3].Date);
        var wave5 = VolumeBetween(candles, p[4].Date, p[5].Date);
        if (wave3 is null || wave5 is null)
        {
            return Indeterminate("No volume data for wave 3/5 (the market-data provider does not report volume).");
        }

        var confirmed = wave3 > wave5;
        var detail = confirmed
            ? $"Wave 3 volume ({wave3:N0}) exceeds wave 5 volume ({wave5:N0}) — the textbook signature."
            : $"Wave 3 volume ({wave3:N0}) does not exceed wave 5 volume ({wave5:N0}).";
        return new RuleResult(RuleName, confirmed ? RuleStatus.Pass : RuleStatus.Fail, detail) { IsGuideline = true };
    }

    private static RuleResult CheckCorrection(IReadOnlyList<WaveAnnotation> p, IReadOnlyList<MarketCandle> candles)
    {
        var waveA = VolumeBetween(candles, p[0].Date, p[1].Date);
        var waveB = VolumeBetween(candles, p[1].Date, p[2].Date);
        var waveC = VolumeBetween(candles, p[2].Date, p[3].Date);
        if (waveA is null || waveB is null || waveC is null)
        {
            return Indeterminate("No volume data for waves A/B/C (the market-data provider does not report volume).");
        }

        var confirmed = waveB < waveA && waveB < waveC;
        var detail = confirmed
            ? $"Wave B volume ({waveB:N0}) contracts against A ({waveA:N0}) and C ({waveC:N0}) — the textbook signature."
            : $"Wave B volume ({waveB:N0}) does not contract against both A ({waveA:N0}) and C ({waveC:N0}).";
        return new RuleResult(RuleName, confirmed ? RuleStatus.Pass : RuleStatus.Fail, detail) { IsGuideline = true };
    }

    /// <summary>
    /// Sums volume over the candles forming a wave's own move (strictly after <paramref name="from"/>,
    /// up to and including <paramref name="to"/>). Null when every candle in range reports zero
    /// volume — a provider that doesn't supply it, distinct from a wave that genuinely traded on
    /// low turnout (AC2).
    /// </summary>
    private static decimal? VolumeBetween(IReadOnlyList<MarketCandle> candles, DateTime from, DateTime to)
    {
        var inRange = candles.Where(c => c.OpenTime > from && c.OpenTime <= to).ToList();
        return inRange.Count == 0 || inRange.All(c => c.Volume == 0) ? null : inRange.Sum(c => c.Volume);
    }

    private static RuleResult Indeterminate(string detail) =>
        new(RuleName, RuleStatus.Indeterminate, detail) { IsGuideline = true };
}
