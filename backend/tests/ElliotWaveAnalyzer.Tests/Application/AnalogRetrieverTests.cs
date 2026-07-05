using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Retrieval: the k nearest by feature similarity (AC1), <b>only concluded</b> setups, the
/// <b>no-lookahead</b> constraint that nothing concluding on/after the as-of date can appear (AC3),
/// and identical results on repeat (AC5).
/// </summary>
[TestFixture]
public sealed class AnalogRetrieverTests
{
    private static readonly DateTimeOffset AsOf = new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private static SetupFeatures Features(
        StructureKind structure = StructureKind.Impulse,
        bool bullish = true,
        double score = 0.7,
        double confluence = 0.5,
        double rewardToRisk = 2.0,
        double distance = 0.08,
        double rsi = 0.55,
        double macd = 0.6) =>
        new(structure, bullish, "1d", score, confluence, rewardToRisk, distance, rsi, macd);

    private static HistoricalSetup Setup(
        string symbol,
        int daysBeforeAsOf,
        AnalysisOutcome outcome,
        SetupFeatures features,
        int resolutionDays = 10)
    {
        var concluded = outcome == AnalysisOutcome.Pending
            ? (DateTimeOffset?)null
            : AsOf.AddDays(-daysBeforeAsOf);
        var formed = (concluded ?? AsOf).AddDays(-resolutionDays);
        return new HistoricalSetup(symbol, formed, concluded, outcome, features);
    }

    [Test]
    public void Retrieve_RanksNearestFirstByFeatureSimilarity()
    {
        var query = Features(StructureKind.Impulse, bullish: true, score: 0.8);
        var corpus = new[]
        {
            Setup("EXACT", 30, AnalysisOutcome.TargetReached, Features(StructureKind.Impulse, true, 0.8)),
            Setup("CLOSE", 40, AnalysisOutcome.TargetReached, Features(StructureKind.Impulse, true, 0.6)),
            Setup("WRONG", 50, AnalysisOutcome.Invalidated, Features(StructureKind.Zigzag, false, 0.2)),
        };

        var analogs = AnalogRetriever.Retrieve(query, corpus, AsOf, k: 3);

        Assert.That(analogs.Select(a => a.Setup.Symbol), Is.EqualTo(new[] { "EXACT", "CLOSE", "WRONG" }));
        Assert.That(analogs[0].Similarity, Is.GreaterThan(analogs[1].Similarity));
        Assert.That(analogs[1].Similarity, Is.GreaterThan(analogs[2].Similarity));
    }

    [Test]
    public void Retrieve_ExcludesPendingSetups()
    {
        var query = Features();
        var corpus = new[]
        {
            Setup("OPEN", 30, AnalysisOutcome.Pending, Features()),
            Setup("DONE", 40, AnalysisOutcome.TargetReached, Features()),
        };

        var analogs = AnalogRetriever.Retrieve(query, corpus, AsOf, k: 5);

        Assert.That(analogs.Select(a => a.Setup.Symbol), Is.EqualTo(new[] { "DONE" }));
    }

    [Test]
    public void Retrieve_NoLookahead_ExcludesSetupsConcludedOnOrAfterAsOf()
    {
        var query = Features();
        var corpus = new[]
        {
            Setup("BEFORE", 1, AnalysisOutcome.TargetReached, Features()),
            // concluded exactly at as-of, and after — both must be invisible.
            new HistoricalSetup("AT", AsOf.AddDays(-5), AsOf, AnalysisOutcome.TargetReached, Features()),
            new HistoricalSetup("AFTER", AsOf, AsOf.AddDays(3), AnalysisOutcome.Invalidated, Features()),
        };

        var analogs = AnalogRetriever.Retrieve(query, corpus, AsOf, k: 10);

        Assert.Multiple(() =>
        {
            Assert.That(analogs.Select(a => a.Setup.Symbol), Is.EqualTo(new[] { "BEFORE" }));
            Assert.That(analogs.All(a => a.Setup.ConcludedAt!.Value < AsOf), Is.True);
            Assert.That(analogs.Count(a => a.Setup.ConcludedAt!.Value >= AsOf), Is.Zero);
        });
    }

    [Test]
    public void Retrieve_LimitsToK()
    {
        var query = Features();
        var corpus = Enumerable.Range(0, 20)
            .Select(i => Setup($"S{i:D2}", 10 + i, AnalysisOutcome.TargetReached, Features(score: 0.5 + i * 0.01)))
            .ToArray();

        Assert.That(AnalogRetriever.Retrieve(query, corpus, AsOf, k: 7), Has.Count.EqualTo(7));
    }

    [Test]
    public void Retrieve_NonPositiveK_ReturnsEmpty()
    {
        Assert.That(AnalogRetriever.Retrieve(Features(), [Setup("X", 5, AnalysisOutcome.TargetReached, Features())], AsOf, 0),
            Is.Empty);
    }

    [Test]
    public void Retrieve_SameQueryAndCorpus_IsDeterministic()
    {
        var query = Features();
        // Two setups with identical features force the deterministic tie-break (oldest, then symbol).
        var corpus = new[]
        {
            Setup("BBB", 30, AnalysisOutcome.TargetReached, Features(), resolutionDays: 8),
            Setup("AAA", 30, AnalysisOutcome.TargetReached, Features(), resolutionDays: 12),
            Setup("CCC", 20, AnalysisOutcome.Invalidated, Features(score: 0.4)),
        };

        var first = AnalogRetriever.Retrieve(query, corpus, AsOf, k: 3);
        var second = AnalogRetriever.Retrieve(query, corpus, AsOf, k: 3);

        Assert.That(
            first.Select(a => (a.Setup.Symbol, a.Similarity)),
            Is.EqualTo(second.Select(a => (a.Setup.Symbol, a.Similarity))));
    }
}
