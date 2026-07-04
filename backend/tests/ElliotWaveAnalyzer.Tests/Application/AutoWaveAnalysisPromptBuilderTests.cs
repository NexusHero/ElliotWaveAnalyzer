using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Unit tests for <see cref="AutoWaveAnalysisPromptBuilder"/>. The builder is pure (static, no
/// I/O), so these exercise the branches the ensemble tests never reach: the market-context block
/// (candles present), the Fibonacci-ratio list, and the nested-subdivision tree rendering.
/// </summary>
[TestFixture]
public sealed class AutoWaveAnalysisPromptBuilderTests
{
    private static readonly DateTime Day = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static WaveAnnotation P(int dayOffset, decimal price, string label) =>
        new(Day.AddDays(dayOffset), price, label);

    private static MarketCandle Candle(int dayOffset, decimal close) =>
        new(Day.AddDays(dayOffset), close, close + 5m, close - 5m, close, 0m);

    private static WaveNode Terminal(string label, int fromDay, decimal fromPrice, int toDay, decimal toPrice) =>
        new(label, null, WaveDegree.Minor, P(fromDay, fromPrice, label), P(toDay, toPrice, label), null, 0.5m, []);

    /// <summary>A rich candidate: guideline score, rules, ratios and a subdivided tree.</summary>
    private static WaveCandidate RichCandidate()
    {
        var report = new WaveRuleReport(
            BullishAssumed: true,
            Rules:
            [
                new RuleResult("Wave 2 never retraces 100% of wave 1", RuleStatus.Pass, "ok"),
                new RuleResult("Wave 3 is never the shortest", RuleStatus.Pass, "ok"),
            ],
            Ratios:
            [
                new FibRatio("wave2/wave1", 0.618m),
                new FibRatio("wave3/wave1", 1.618m),
            ]);

        // Root has one subdivided child (itself holding a terminal leg) → triggers the tree block.
        var subdividedWaveOne = new WaveNode(
            "1", StructureKind.Impulse, WaveDegree.Intermediate,
            P(0, 100m, "1"), P(1, 120m, "1"), null, 0.72m,
            [Terminal("i", 0, 100m, 1, 120m)]);

        var tree = new WaveNode(
            "Impulse", StructureKind.Impulse, WaveDegree.Primary,
            P(0, 100m, "Impulse"), P(5, 170m, "Impulse"), report, 0.81m,
            [subdividedWaveOne]);

        return new WaveCandidate(
            0,
            "Impulse",
            P(0, 100m, "0"),
            [P(1, 120m, "1"), P(2, 110m, "2"), P(3, 150m, "3"), P(4, 130m, "4"), P(5, 170m, "5")],
            report,
            null)
        {
            Tree = tree,
            Score = 0.81m,
        };
    }

    /// <summary>A minimal (legacy) candidate: no score, no ratios, no tree, bearish.</summary>
    private static WaveCandidate MinimalCandidate() =>
        new(
            1,
            "Impulse",
            P(0, 200m, "0"),
            [P(1, 180m, "1"), P(2, 190m, "2"), P(3, 150m, "3"), P(4, 170m, "4"), P(5, 130m, "5")],
            new WaveRuleReport(BullishAssumed: false, Rules: [], Ratios: []),
            null);

    [Test]
    public void Build_WithCandles_IncludesMarketContext()
    {
        var candles = new[] { Candle(0, 100m), Candle(1, 130m), Candle(2, 170m) };

        var prompt = AutoWaveAnalysisPromptBuilder.Build("BTC", candles, [RichCandidate()]);

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("## Market Context"));
            Assert.That(prompt, Does.Contain("Symbol:       BTC/USD"));
            Assert.That(prompt, Does.Contain("(low)").And.Contains("(high)"));
        });
    }

    [Test]
    public void Build_NoCandles_OmitsMarketContext()
    {
        var prompt = AutoWaveAnalysisPromptBuilder.Build("ETH", [], [MinimalCandidate()]);

        Assert.That(prompt, Does.Not.Contain("## Market Context"));
    }

    [Test]
    public void Build_RichCandidate_RendersScoreRatiosRulesAndSubdivision()
    {
        var prompt = AutoWaveAnalysisPromptBuilder.Build("BTC", [], [RichCandidate()]);

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("Candidate 0"));
            Assert.That(prompt, Does.Contain("bullish"));
            Assert.That(prompt, Does.Contain("guideline score"));
            Assert.That(prompt, Does.Contain("fib: wave2/wave1"));
            Assert.That(prompt, Does.Contain("[Pass]"));
            Assert.That(prompt, Does.Contain("internal subdivision"));
            // AppendTree recursion: the subdivided wave and its terminal leg both appear.
            Assert.That(prompt, Does.Contain("wave 1:").And.Contains("wave i:"));
        });
    }

    [Test]
    public void Build_MinimalCandidate_OmitsScoreAndSubdivision()
    {
        var prompt = AutoWaveAnalysisPromptBuilder.Build("BTC", [], [MinimalCandidate()]);

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("Candidate 1"));
            Assert.That(prompt, Does.Contain("bearish"));
            Assert.That(prompt, Does.Not.Contain("guideline score"));
            Assert.That(prompt, Does.Not.Contain("internal subdivision"));
            Assert.That(prompt, Does.Not.Contain("fib:"));
        });
    }

    [Test]
    public void Build_AlwaysIncludesResponseSchema()
    {
        var prompt = AutoWaveAnalysisPromptBuilder.Build("BTC", [], [MinimalCandidate()]);

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("## Required Output"));
            Assert.That(prompt, Does.Contain("\"bestCandidateId\""));
        });
    }
}
