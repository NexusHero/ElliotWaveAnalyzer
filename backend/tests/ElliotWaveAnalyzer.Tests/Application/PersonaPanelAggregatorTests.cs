using System.Text.Json;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// <see cref="PersonaPanelAggregator"/>: weighted consensus, reproducibility (AC2), an
/// out-of-vocabulary candidate never surfacing (AC1), and disagreement surfaced as a low consensus
/// score vs. full agreement as a high one (AC4).
/// </summary>
[TestFixture]
public sealed class PersonaPanelAggregatorTests
{
    private static RankedCandidate Rc(int id, string confidence = "medium", string rationale = "r", string outlook = "o") =>
        new(id, confidence, rationale, outlook);

    private static PersonaRanking Ranking(string persona, int bestId, params RankedCandidate[] all) =>
        new(persona, new AutoWaveRanking(bestId, "summary", all));

    [Test]
    public void Aggregate_UnanimousPersonas_FullConsensus()
    {
        var rankings = new[]
        {
            Ranking("Conservative", 1, Rc(1), Rc(2)),
            Ranking("Aggressive", 1, Rc(1), Rc(2)),
            Ranking("Contrarian", 1, Rc(1), Rc(2)),
        };
        var weights = new[]
        {
            new PersonaWeight("Conservative", 0.7, false),
            new PersonaWeight("Aggressive", 0.6, false),
            new PersonaWeight("Contrarian", 0.5, true),
        };

        var result = PersonaPanelAggregator.Aggregate(rankings, weights, [1, 2]);

        Assert.Multiple(() =>
        {
            Assert.That(result.BestCandidateId, Is.EqualTo(1));
            Assert.That(result.ConsensusScore, Is.EqualTo(1.0));
        });
    }

    [Test]
    public void Aggregate_SplitPersonas_LowConsensusScore_NotHidden()
    {
        // Two personas favour #1, one (heavier) favours #2 — the panel is genuinely split.
        var rankings = new[]
        {
            Ranking("Conservative", 1, Rc(1), Rc(2)),
            Ranking("Aggressive", 1, Rc(1), Rc(2)),
            Ranking("Contrarian", 2, Rc(1), Rc(2)),
        };
        var weights = new[]
        {
            new PersonaWeight("Conservative", 0.3, false),
            new PersonaWeight("Aggressive", 0.3, false),
            new PersonaWeight("Contrarian", 0.4, false),
        };

        var result = PersonaPanelAggregator.Aggregate(rankings, weights, [1, 2]);

        // #1 has 0.6 of 1.0 total weight — favoured, but far from unanimous.
        Assert.Multiple(() =>
        {
            Assert.That(result.BestCandidateId, Is.EqualTo(1));
            Assert.That(result.ConsensusScore, Is.EqualTo(0.6).Within(0.001));
            Assert.That(result.ConsensusScore, Is.LessThan(1.0));
        });
    }

    [Test]
    public void Aggregate_SameRankingsAndWeights_IsReproducible()
    {
        var rankings = new[]
        {
            Ranking("Conservative", 1, Rc(1), Rc(2)),
            Ranking("Aggressive", 2, Rc(1), Rc(2)),
        };
        var weights = new[]
        {
            new PersonaWeight("Conservative", 0.5, false),
            new PersonaWeight("Aggressive", 0.5, false),
        };

        var first = PersonaPanelAggregator.Aggregate(rankings, weights, [1, 2]);
        var second = PersonaPanelAggregator.Aggregate(rankings, weights, [1, 2]);

        // Records holding List<T> members compare by reference, not content — serialize instead
        // (same trick used elsewhere for this exact records+List gotcha).
        Assert.That(JsonSerializer.Serialize(first), Is.EqualTo(JsonSerializer.Serialize(second)));
    }

    [Test]
    public void Aggregate_TiedWeight_BreaksDeterministicallyByLowestId()
    {
        var rankings = new[]
        {
            Ranking("Conservative", 2, Rc(1), Rc(2)),
            Ranking("Aggressive", 1, Rc(1), Rc(2)),
        };
        var weights = new[]
        {
            new PersonaWeight("Conservative", 0.5, false),
            new PersonaWeight("Aggressive", 0.5, false),
        };

        var result = PersonaPanelAggregator.Aggregate(rankings, weights, [1, 2]);

        Assert.That(result.BestCandidateId, Is.EqualTo(1));
    }

    [Test]
    public void Aggregate_PersonaPicksAnOutOfVocabularyCandidate_NeverSurfaces()
    {
        // Persona "Aggressive" picks id 99, which was never in the deterministic candidate set (a
        // hallucination or a stale reference) — it must never win, and must never appear in Rankings.
        var rankings = new[]
        {
            Ranking("Conservative", 1, Rc(1), Rc(2)),
            Ranking("Aggressive", 99, Rc(1), Rc(99)),
        };
        var weights = new[]
        {
            new PersonaWeight("Conservative", 0.5, false),
            new PersonaWeight("Aggressive", 0.9, false), // heavier weight — would win if not filtered
        };

        var result = PersonaPanelAggregator.Aggregate(rankings, weights, [1, 2]);

        Assert.Multiple(() =>
        {
            Assert.That(result.BestCandidateId, Is.EqualTo(1));
            Assert.That(result.Rankings.Select(r => r.CandidateId), Does.Not.Contain(99));
        });
    }

    [Test]
    public void Aggregate_MergesRationalesFromEveryPersonaThatRankedTheCandidate()
    {
        var rankings = new[]
        {
            Ranking("Conservative", 1, Rc(1, rationale: "cautious read")),
            Ranking("Aggressive", 1, Rc(1, rationale: "bold read")),
        };
        var weights = new[]
        {
            new PersonaWeight("Conservative", 0.5, false),
            new PersonaWeight("Aggressive", 0.5, false),
        };

        var result = PersonaPanelAggregator.Aggregate(rankings, weights, [1]);

        var merged = result.Rankings.Single();
        Assert.Multiple(() =>
        {
            Assert.That(merged.Rationale, Does.Contain("[Conservative] cautious read"));
            Assert.That(merged.Rationale, Does.Contain("[Aggressive] bold read"));
        });
    }

    [Test]
    public void NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => PersonaPanelAggregator.Aggregate(null!, [], []));
        Assert.Throws<ArgumentNullException>(() => PersonaPanelAggregator.Aggregate([], null!, []));
        Assert.Throws<ArgumentNullException>(() => PersonaPanelAggregator.Aggregate([], [], null!));
    }
}
