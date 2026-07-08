using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using NSubstitute;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// #224 AC4: when <c>candles</c> is supplied to <see cref="WaveCandidateGenerator.GenerateParsed"/>,
/// the momentum/volume guidelines are appended to the candidate's <see cref="WaveRuleReport"/> and a
/// failed guideline measurably reduces <see cref="WaveCandidate.Score"/> by the same
/// <see cref="WaveScoringOptions.GuidelinePenalty"/> a failed guideline already applies inside the
/// parser — hard-rule validity (a candidate surviving generation at all) is unaffected either way.
/// </summary>
[TestFixture]
public sealed class WaveCandidateGeneratorMomentumVolumeTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // A plain, flat 5-wave impulse: 100 -> 120 (w1) -> 110 (w2) -> 150 (w3) -> 130 (w4) -> 170 (w5).
    private static IReadOnlyList<SwingPivot> Impulse() =>
    [
        new(Start, 100m, IsHigh: false),
        new(Start.AddDays(1), 120m, IsHigh: true),
        new(Start.AddDays(2), 110m, IsHigh: false),
        new(Start.AddDays(3), 150m, IsHigh: true),
        new(Start.AddDays(4), 130m, IsHigh: false),
        new(Start.AddDays(5), 170m, IsHigh: true),
    ];

    private static MarketCandle Candle(DateTime date, decimal volume) => new(date, 100m, 101m, 99m, 100m, volume);

    [Test]
    public void NoCandles_NoGuidelineRowsAdded_ScoreUnchanged()
    {
        var (candidates, _) = WaveCandidateGenerator.GenerateParsed(Impulse());

        Assert.That(candidates, Is.Not.Empty);
        var rules = candidates[0].RuleReport.Rules;
        Assert.That(rules.Any(r => r.Name == MomentumDivergenceChecker.RuleName), Is.False);
        Assert.That(rules.Any(r => r.Name == VolumeGuidelineChecker.RuleName), Is.False);
    }

    [Test]
    public void WithCandlesAndAFailingVolumeGuideline_ScoreIsPenalized_AndRuleRowsAppear_AC4()
    {
        var pivots = Impulse();
        // Wave 5 volume exceeds wave 3's — the non-confirming case, so the volume guideline fails.
        var candles = new List<MarketCandle>
        {
            Candle(pivots[3].Date, 200m), // wave 3
            Candle(pivots[5].Date, 900m), // wave 5
        };

        var (baseline, _) = WaveCandidateGenerator.GenerateParsed(pivots);
        var (withGuidelines, _) = WaveCandidateGenerator.GenerateParsed(
            pivots, candles: candles, indicatorCalculator: Substitute.For<IIndicatorCalculator>());

        // The beam search may also find a higher-scoring alternate (e.g. a subdivided Zigzag) over
        // the same pivot range — pick the flat 5-wave Impulse interpretation specifically, since
        // that is the one this fixture's wave-3/wave-5 dates were built to describe.
        var baselineImpulse = baseline.Single(c => c.Structure == "Impulse");
        var withGuidelinesImpulse = withGuidelines.Single(c => c.Structure == "Impulse");

        var rules = withGuidelinesImpulse.RuleReport.Rules;
        var volumeRow = rules.Single(r => r.Name == VolumeGuidelineChecker.RuleName);

        Assert.Multiple(() =>
        {
            Assert.That(rules.Any(r => r.Name == MomentumDivergenceChecker.RuleName), Is.True);
            Assert.That(volumeRow.Status, Is.EqualTo(RuleStatus.Fail));
            Assert.That(volumeRow.IsGuideline, Is.True);
            Assert.That(
                withGuidelinesImpulse.Score, Is.LessThan(baselineImpulse.Score),
                "a failed guideline must measurably reduce the score");
            Assert.That(
                withGuidelinesImpulse.RuleReport.Rules.Any(r => r is { Status: RuleStatus.Fail, IsGuideline: false }),
                Is.False, "hard-rule validity must be unaffected by these guidelines");
        });
    }
}
