using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Unit tests for the corrective/diagonal structure checkers (zigzag, flat, triangle,
/// diagonal). Each fixture covers a clean pass, one violation per rule, and the bearish
/// mirror, since every rule is direction-dependent.
/// </summary>
[TestFixture]
public sealed class CorrectiveRuleCheckerTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Positional pivots one day apart; labels are placeholders (checkers are positional).</summary>
    private static IReadOnlyList<WaveAnnotation> Points(params decimal[] prices)
        => [.. prices.Select((p, i) => new WaveAnnotation(Start.AddDays(i), p, "1"))];

    private static RuleResult Rule(WaveRuleReport report, int index) => report.Rules[index];

    // ─── Zigzag ────────────────────────────────────────────────────────────────

    [Test]
    public void Zigzag_CleanBullish_PassesBothRules()
    {
        // P0=100 → A=130 → B=112 (holds origin) → C=140 (beyond A).
        var report = ZigzagRuleChecker.Check(Points(100m, 130m, 112m, 140m));

        Assert.Multiple(() =>
        {
            Assert.That(report.BullishAssumed, Is.True);
            Assert.That(report.Rules.Select(r => r.Status), Is.All.EqualTo(RuleStatus.Pass));
            Assert.That(Rule(report, 0).IsGuideline, Is.False, "B-holds-origin is a hard rule");
            Assert.That(Rule(report, 1).IsGuideline, Is.True, "C-beyond-A is a guideline");
        });
    }

    [Test]
    public void Zigzag_CleanBearish_PassesBothRules()
    {
        // Mirror: down zigzag 100 → 70 → 88 → 60.
        var report = ZigzagRuleChecker.Check(Points(100m, 70m, 88m, 60m));

        Assert.Multiple(() =>
        {
            Assert.That(report.BullishAssumed, Is.False);
            Assert.That(report.Rules.Select(r => r.Status), Is.All.EqualTo(RuleStatus.Pass));
        });
    }

    [Test]
    public void Zigzag_BBeyondOrigin_FailsHardRule()
    {
        var report = ZigzagRuleChecker.Check(Points(100m, 130m, 95m, 140m));

        Assert.Multiple(() =>
        {
            Assert.That(Rule(report, 0).Status, Is.EqualTo(RuleStatus.Fail));
            Assert.That(Rule(report, 0).IsGuideline, Is.False);
        });
    }

    [Test]
    public void Zigzag_TruncatedC_FailsGuidelineOnly()
    {
        var report = ZigzagRuleChecker.Check(Points(100m, 130m, 112m, 128m));

        Assert.Multiple(() =>
        {
            Assert.That(Rule(report, 0).Status, Is.EqualTo(RuleStatus.Pass));
            Assert.That(Rule(report, 1).Status, Is.EqualTo(RuleStatus.Fail));
            Assert.That(Rule(report, 1).IsGuideline, Is.True,
                "a truncated C must not hard-invalidate the count");
        });
    }

    [Test]
    public void Zigzag_MissingPivots_ReportsIndeterminate()
    {
        var report = ZigzagRuleChecker.Check(Points(100m, 130m));

        Assert.That(report.Rules.Select(r => r.Status), Is.All.EqualTo(RuleStatus.Indeterminate));
    }

    [Test]
    public void Zigzag_ComputesBRetraceAndCExtensionRatios()
    {
        var report = ZigzagRuleChecker.Check(Points(100m, 130m, 112m, 140m));

        Assert.Multiple(() =>
        {
            Assert.That(report.Ratios[0].Ratio, Is.EqualTo(0.6m));   // (130-112)/30
            Assert.That(report.Ratios[1].Ratio, Is.EqualTo(0.933m)); // (140-112)/30
        });
    }

    // ─── Flat ──────────────────────────────────────────────────────────────────

    [Test]
    public void Flat_RegularBullish_PassesAndClassifies()
    {
        // A: 100→130, B: deep 95% retrace to 101.5, C: beyond A to 135.
        var points = Points(100m, 130m, 101.5m, 135m);

        var report = FlatRuleChecker.Check(points);

        Assert.Multiple(() =>
        {
            Assert.That(report.Rules.Select(r => r.Status), Is.All.EqualTo(RuleStatus.Pass));
            Assert.That(FlatRuleChecker.Classify(points), Is.EqualTo(FlatVariant.Regular));
        });
    }

    [Test]
    public void Flat_ShallowB_FailsHardRule()
    {
        // B retraces only 50% — that's a zigzag, not a flat.
        var report = FlatRuleChecker.Check(Points(100m, 130m, 115m, 135m));

        Assert.Multiple(() =>
        {
            Assert.That(Rule(report, 0).Status, Is.EqualTo(RuleStatus.Fail));
            Assert.That(Rule(report, 0).IsGuideline, Is.False);
        });
    }

    [Test]
    public void Flat_ExpandedVariant_BBeyondOriginAndCBeyondA()
    {
        // B overshoots the origin (110% of A), C still travels beyond A.
        var points = Points(100m, 130m, 97m, 136m);

        var report = FlatRuleChecker.Check(points);

        Assert.Multiple(() =>
        {
            Assert.That(report.Rules.Select(r => r.Status), Is.All.EqualTo(RuleStatus.Pass));
            Assert.That(FlatRuleChecker.Classify(points), Is.EqualTo(FlatVariant.Expanded));
        });
    }

    [Test]
    public void Flat_RunningVariant_CFallsShort_FailsGuidelineOnly()
    {
        var points = Points(100m, 130m, 97m, 125m);

        var report = FlatRuleChecker.Check(points);

        Assert.Multiple(() =>
        {
            Assert.That(Rule(report, 0).Status, Is.EqualTo(RuleStatus.Pass));
            Assert.That(Rule(report, 1).Status, Is.EqualTo(RuleStatus.Fail));
            Assert.That(Rule(report, 1).IsGuideline, Is.True);
            Assert.That(FlatRuleChecker.Classify(points), Is.EqualTo(FlatVariant.Running));
        });
    }

    [Test]
    public void Flat_BearishMirror_PassesAndClassifies()
    {
        // Down flat: A: 100→70, B: back up to 98.5 (95%), C: below A to 65.
        var points = Points(100m, 70m, 98.5m, 65m);

        var report = FlatRuleChecker.Check(points);

        Assert.Multiple(() =>
        {
            Assert.That(report.BullishAssumed, Is.False);
            Assert.That(report.Rules.Select(r => r.Status), Is.All.EqualTo(RuleStatus.Pass));
            Assert.That(FlatRuleChecker.Classify(points), Is.EqualTo(FlatVariant.Regular));
        });
    }

    [Test]
    public void Flat_MissingPivots_ClassifyReturnsNull()
    {
        Assert.That(FlatRuleChecker.Classify(Points(100m, 130m, 101.5m)), Is.Null);
    }

    // ─── Triangle ──────────────────────────────────────────────────────────────

    [Test]
    public void Triangle_CleanContracting_PassesAllLegs()
    {
        // Legs: A=30, B=18, C=14, D=10, E=7 — every same-direction leg contracts.
        var report = TriangleRuleChecker.Check(Points(100m, 130m, 112m, 126m, 116m, 123m));

        Assert.That(report.Rules.Select(r => r.Status), Is.All.EqualTo(RuleStatus.Pass));
    }

    [Test]
    public void Triangle_NonContractingE_FailsThirdRule()
    {
        // E = 15 > C = 14: the boundary lines cannot converge.
        var report = TriangleRuleChecker.Check(Points(100m, 130m, 112m, 126m, 116m, 131m));

        Assert.Multiple(() =>
        {
            Assert.That(Rule(report, 0).Status, Is.EqualTo(RuleStatus.Pass));
            Assert.That(Rule(report, 1).Status, Is.EqualTo(RuleStatus.Pass));
            Assert.That(Rule(report, 2).Status, Is.EqualTo(RuleStatus.Fail));
        });
    }

    [Test]
    public void Triangle_PartialStructure_ReportsIndeterminateForMissingLegs()
    {
        var report = TriangleRuleChecker.Check(Points(100m, 130m, 112m, 126m));

        Assert.Multiple(() =>
        {
            Assert.That(Rule(report, 0).Status, Is.EqualTo(RuleStatus.Pass));
            Assert.That(Rule(report, 1).Status, Is.EqualTo(RuleStatus.Indeterminate));
            Assert.That(Rule(report, 2).Status, Is.EqualTo(RuleStatus.Indeterminate));
        });
    }

    [Test]
    public void Triangle_BearishMirror_Passes()
    {
        var report = TriangleRuleChecker.Check(Points(100m, 70m, 88m, 74m, 84m, 77m));

        Assert.Multiple(() =>
        {
            Assert.That(report.BullishAssumed, Is.False);
            Assert.That(report.Rules.Select(r => r.Status), Is.All.EqualTo(RuleStatus.Pass));
        });
    }

    [Test]
    public void Triangle_ComputesLegContractionRatios()
    {
        var report = TriangleRuleChecker.Check(Points(100m, 130m, 112m, 126m, 116m, 123m));

        Assert.Multiple(() =>
        {
            Assert.That(report.Ratios[0].Ratio, Is.EqualTo(0.467m)); // C/A = 14/30
            Assert.That(report.Ratios[1].Ratio, Is.EqualTo(0.556m)); // D/B = 10/18
            Assert.That(report.Ratios[2].Ratio, Is.EqualTo(0.5m));   // E/C = 7/14
        });
    }

    // ─── Diagonal ──────────────────────────────────────────────────────────────

    /// <summary>w1=30, w2=18, w3=26, w4=14 (overlaps wave 1), w5=18 — a clean wedge.</summary>
    private static IReadOnlyList<WaveAnnotation> CleanBullishDiagonal()
        => Points(100m, 130m, 112m, 138m, 124m, 142m);

    [Test]
    public void Diagonal_CleanBullishWedge_PassesAllRules()
    {
        var report = DiagonalRuleChecker.Check(CleanBullishDiagonal());

        Assert.That(report.Rules.Select(r => r.Status), Is.All.EqualTo(RuleStatus.Pass));
    }

    [Test]
    public void Diagonal_Wave4Overlap_IsAllowed_WhereImpulseRulesReject()
    {
        // The same pivots fail the impulse checker (wave 4 at 124 overlaps wave 1's end at
        // 130) but are a valid diagonal — the false negative this checker exists to fix.
        var pivots = CleanBullishDiagonal();

        var impulse = ElliottRuleChecker.Check(pivots);
        var diagonal = DiagonalRuleChecker.Check(pivots);

        Assert.Multiple(() =>
        {
            Assert.That(impulse.Rules.Any(r => r.Status == RuleStatus.Fail), Is.True);
            Assert.That(diagonal.Rules.Select(r => r.Status), Is.All.EqualTo(RuleStatus.Pass));
        });
    }

    [Test]
    public void Diagonal_Wave2BeyondOrigin_FailsFirstRule()
    {
        var report = DiagonalRuleChecker.Check(Points(100m, 130m, 95m, 138m, 124m, 142m));

        Assert.That(Rule(report, 0).Status, Is.EqualTo(RuleStatus.Fail));
    }

    [Test]
    public void Diagonal_NonContractingWave4_FailsWedgeRule()
    {
        // w4=20 > w2=18 while everything else is fine: only the wedge rule fails.
        var report = DiagonalRuleChecker.Check(Points(100m, 130m, 112m, 138m, 118m, 138m));

        Assert.Multiple(() =>
        {
            Assert.That(Rule(report, 0).Status, Is.EqualTo(RuleStatus.Pass));
            Assert.That(Rule(report, 1).Status, Is.EqualTo(RuleStatus.Pass));
            Assert.That(Rule(report, 2).Status, Is.EqualTo(RuleStatus.Fail));
        });
    }

    [Test]
    public void Diagonal_BearishMirror_Passes()
    {
        var report = DiagonalRuleChecker.Check(Points(100m, 70m, 88m, 62m, 76m, 58m));

        Assert.Multiple(() =>
        {
            Assert.That(report.BullishAssumed, Is.False);
            Assert.That(report.Rules.Select(r => r.Status), Is.All.EqualTo(RuleStatus.Pass));
        });
    }

    [Test]
    public void Diagonal_MissingPivots_ReportsIndeterminate()
    {
        var report = DiagonalRuleChecker.Check(Points(100m, 130m));

        Assert.That(report.Rules.Select(r => r.Status), Is.All.EqualTo(RuleStatus.Indeterminate));
    }
}
