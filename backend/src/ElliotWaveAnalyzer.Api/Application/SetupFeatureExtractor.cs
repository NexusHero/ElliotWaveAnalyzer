using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Turns a formed count's deterministic geometry into the <see cref="SetupFeatures"/> fingerprint the
/// analog search compares. Everything here is computed by the engine — direction and confluence from
/// the <see cref="WaveLevels"/>, reward:risk and distance-to-invalidation from the same
/// <see cref="RiskCalculator"/> the risk box uses (entry = current price) — so the query setup and
/// every historical setup are described on identical terms. The momentum regimes (RSI/MACD) are passed
/// in already normalised to [0, 1], since they come from the indicator calculator, not the geometry.
/// Pure and static.
/// </summary>
public static class SetupFeatureExtractor
{
    /// <summary>Builds the feature fingerprint for a count at <paramref name="currentPrice"/>.</summary>
    public static SetupFeatures Extract(
        StructureKind structure,
        WaveLevels levels,
        decimal score,
        decimal currentPrice,
        double rsiRegime,
        double macdRegime,
        string timeframe)
    {
        ArgumentNullException.ThrowIfNull(levels);

        var rewardToRisk = 0.0;
        var distancePct = 0.0;

        if (levels.Invalidation is { } invalidation && currentPrice > 0)
        {
            // The reward side uses the target edge the move would touch first (the near edge in the
            // trade's direction), matching how the risk box maps a target zone to a price.
            var targets = levels.TargetZones.Count > 0
                ? new[] { levels.Bullish ? levels.TargetZones[0].Low : levels.TargetZones[0].High }
                : [];

            var risk = RiskCalculator.Assess(currentPrice, invalidation.Price, targets, levels.Bullish, 1m);
            if (risk.HasValidStop)
            {
                distancePct = (double)risk.StopDistancePct;
                if (risk.Targets.Count > 0)
                {
                    rewardToRisk = (double)risk.Targets[0].RewardToRisk;
                }
            }
        }

        return new SetupFeatures(
            structure,
            levels.Bullish,
            timeframe,
            (double)score,
            ConfluenceStrength(levels.ConfluenceZones),
            rewardToRisk,
            distancePct,
            rsiRegime,
            macdRegime);
    }

    // Strength of the strongest confluence zone on the same none/weak/strong basis the backtest uses:
    // a multi-ratio stack (≥ 2 contributing levels) is "strong" (1.0), a lone level "weak" (0.5).
    private static double ConfluenceStrength(IReadOnlyList<ConfluenceZone> zones)
    {
        if (zones.Count == 0) return 0.0;
        var top = zones.MaxBy(zone => zone.Score)!;
        return top.Contributions.Count >= 2 ? 1.0 : 0.5;
    }
}
