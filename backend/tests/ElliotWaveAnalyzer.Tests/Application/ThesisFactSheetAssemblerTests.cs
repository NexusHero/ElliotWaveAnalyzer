using System.Text.Json;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// <see cref="ThesisFactSheetAssembler"/>: composes a <see cref="ThesisFactSheet"/> from the engine's
/// already-computed outputs. Pure — every optional input is passed through honestly (never defaulted
/// to a fabricated value), and the same inputs always produce the same fact sheet (AC4).
/// </summary>
[TestFixture]
public sealed class ThesisFactSheetAssemblerTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);

    private static WaveLevels Levels() => new(
        UnfoldingWave: "5",
        Bullish: true,
        Invalidation: new PriceLevel(120.00m, LevelSide.Below, "Invalidation", "End of wave 1"),
        SupportZone: new PriceZone(130.00m, 135.00m, "Entry", "Fib retrace"),
        TargetZones: [new PriceZone(180.00m, 190.00m, "Target", "Fib extension")],
        Alternative: null)
    {
        Scale = FibScale.Linear,
        ConfluenceZones = [new ConfluenceZone(178.00m, 182.00m, 3.5m, ZoneKind.Target, FibScale.Linear, [])],
        Channels = [],
    };

    private static RiskAssessment Risk() => new(
        HasValidStop: true,
        NoStopReason: null,
        Bullish: true,
        Entry: 150.00m,
        StopPrice: 120.00m,
        StopDistanceAbs: 30.00m,
        StopDistancePct: 0.20m,
        RiskCapital: 100m,
        SuggestedSize: 3.33m,
        Notional: 500m,
        Targets: [new TargetRisk(180.00m, 30.00m, 1.0m)]);

    [Test]
    public void Assemble_AllInputsProvided_ComposesEveryFieldFromItsSource()
    {
        var sheet = ThesisFactSheetAssembler.Assemble(
            "ACME", "1W: Impulse → 1D: Zigzag", Levels(), currentPrice: 150.00m, Risk(),
            calibration: new ConfidenceCalibration([], TotalConcluded: 40, OverallHitRate: 0.65m),
            analogs: new AnalogStats(47, 32, 15, 0.68, 12.0, Sufficient: true),
            sentiment: new SentimentReport(true, [], [new MoodDivergence("5", AsOf.DateTime, MoodDivergenceKind.Bearish, 0.5, 0.1)]),
            scenarios: [new Scenario(ScenarioRole.Primary, "Primary", "Impulse", true, 120.00m, false,
                130.00m, 135.00m, 180.00m, 190.00m, "High", 0.8m, 0.65m, ProbabilityBasis.Calibrated, false)],
            AsOf);

        Assert.Multiple(() =>
        {
            Assert.That(sheet.Symbol, Is.EqualTo("ACME"));
            Assert.That(sheet.ChainSummary, Is.EqualTo("1W: Impulse → 1D: Zigzag"));
            Assert.That(sheet.Bullish, Is.True);
            Assert.That(sheet.CurrentPrice, Is.EqualTo(150.00m));
            Assert.That(sheet.Invalidation!.Price, Is.EqualTo(120.00m));
            Assert.That(sheet.EntryZone!.Low, Is.EqualTo(130.00m));
            Assert.That(sheet.TargetZones, Has.Count.EqualTo(1));
            Assert.That(sheet.Scale, Is.EqualTo(FibScale.Linear));
            Assert.That(sheet.Risk!.SuggestedSize, Is.EqualTo(3.33m));
            Assert.That(sheet.ConfluenceZones, Has.Count.EqualTo(1));
            Assert.That(sheet.CalibratedProbability, Is.EqualTo(0.65m));
            Assert.That(sheet.Analogs!.SampleCount, Is.EqualTo(47));
            Assert.That(sheet.SentimentDivergences, Has.Count.EqualTo(1));
            Assert.That(sheet.Scenarios, Has.Count.EqualTo(1));
            Assert.That(sheet.AsOf, Is.EqualTo(AsOf));
        });
    }

    [Test]
    public void Assemble_NoLevels_FallsBackToUnbullishNoZonesLinearScale_NeverFabricated()
    {
        var sheet = ThesisFactSheetAssembler.Assemble(
            "ACME", "No timeframe analyzable", levels: null, currentPrice: null, risk: null,
            calibration: null, analogs: null, sentiment: null, scenarios: null, AsOf);

        Assert.Multiple(() =>
        {
            Assert.That(sheet.Bullish, Is.False);
            Assert.That(sheet.Invalidation, Is.Null);
            Assert.That(sheet.EntryZone, Is.Null);
            Assert.That(sheet.TargetZones, Is.Empty);
            Assert.That(sheet.Scale, Is.EqualTo(FibScale.Linear));
            Assert.That(sheet.ConfluenceZones, Is.Empty);
        });
    }

    [Test]
    public void Assemble_NoValidStopOrNoCoverage_LeavesOptionalSectionsNullNotDefaulted()
    {
        // A count can have levels but no valid stop, no calibration sample, and no analog/sentiment
        // coverage — the fact sheet must say so honestly (null), never invent a number.
        var sheet = ThesisFactSheetAssembler.Assemble(
            "ACME", "1D: Impulse", Levels(), currentPrice: 150.00m, risk: null,
            calibration: null, analogs: null, sentiment: SentimentReport.NoCoverage("no provider"),
            scenarios: null, AsOf);

        Assert.Multiple(() =>
        {
            Assert.That(sheet.Risk, Is.Null);
            Assert.That(sheet.CalibratedProbability, Is.Null);
            Assert.That(sheet.Analogs, Is.Null);
            Assert.That(sheet.SentimentDivergences, Is.Empty);
            Assert.That(sheet.Scenarios, Is.Empty);
        });
    }

    [Test]
    public void Assemble_SameInputsTwice_ProducesAnIdenticalFactSheet()
    {
        // AC4: reproducible facts — same analysis, same fact sheet. List<T> members break Is.EqualTo's
        // structural equality on records (reference equality), so compare via serialization, matching
        // the established trick (WaveVerifierTests, PersonaPanelAggregatorTests).
        var risk = Risk();
        var levels = Levels();
        var first = ThesisFactSheetAssembler.Assemble(
            "ACME", "1W: Impulse", levels, 150.00m, risk, null, null, null, null, AsOf);
        var second = ThesisFactSheetAssembler.Assemble(
            "ACME", "1W: Impulse", levels, 150.00m, risk, null, null, null, null, AsOf);

        Assert.That(JsonSerializer.Serialize(first), Is.EqualTo(JsonSerializer.Serialize(second)));
    }

    [Test]
    public void Assemble_NullSymbolOrChainSummary_Throws()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() => ThesisFactSheetAssembler.Assemble(
                null!, "chain", null, null, null, null, null, null, null, AsOf));
            Assert.Throws<ArgumentNullException>(() => ThesisFactSheetAssembler.Assemble(
                "ACME", null!, null, null, null, null, null, null, null, AsOf));
        });
    }
}
