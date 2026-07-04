using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Tests for the recursive wave grammar parser: golden fixtures with a known intended count,
/// structural invariants (no hard-rule violation ever survives, determinism), and the
/// complexity guard.
/// </summary>
[TestFixture]
public sealed class WaveGrammarParserTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Alternating pivots one day apart, starting with a low.</summary>
    private static IReadOnlyList<SwingPivot> Pivots(params decimal[] prices)
        => [.. prices.Select((p, i) => new SwingPivot(Start.AddDays(i), p, IsHigh: i % 2 == 1))];

    /// <summary>Alternating pivots one day apart, starting with a high.</summary>
    private static IReadOnlyList<SwingPivot> PivotsFromHigh(params decimal[] prices)
        => [.. prices.Select((p, i) => new SwingPivot(Start.AddDays(i), p, IsHigh: i % 2 == 0))];

    /// <summary>
    /// Golden fixture: a bullish impulse whose wave 2 is a zigzag and wave 4 a flat.
    /// 100→130 (w1), zigzag to 112 (w2: 115/122/112), →160 (w3), flat to 143 (w4:
    /// 146/159.5/143, no overlap with w1), →172 (w5).
    /// </summary>
    private static IReadOnlyList<SwingPivot> NestedImpulse()
        => Pivots(100m, 130m, 115m, 122m, 112m, 160m, 146m, 159.5m, 143m, 172m);

    [Test]
    public void Parse_NestedImpulse_TopCandidateIsFullSpanImpulse()
    {
        var result = WaveGrammarParser.Parse(NestedImpulse());

        Assert.That(result.Trees, Is.Not.Empty);
        var top = result.Trees[0].Root;
        Assert.Multiple(() =>
        {
            Assert.That(top.Kind, Is.EqualTo(StructureKind.Impulse));
            Assert.That(top.Start.Price, Is.EqualTo(100m));
            Assert.That(top.End.Price, Is.EqualTo(172m));
            Assert.That(top.Children, Has.Count.EqualTo(5));
            Assert.That(result.SearchTruncated, Is.False);
        });
    }

    [Test]
    public void Parse_NestedImpulse_FindsZigzagW2AndFlatW4Subdivision()
    {
        var result = WaveGrammarParser.Parse(NestedImpulse());

        var intended = result.Trees
            .Select(t => t.Root)
            .Where(r => r.Kind == StructureKind.Impulse && r.Children.Count == 5)
            .FirstOrDefault(r =>
                r.Children[1].Kind == StructureKind.Zigzag
                && r.Children[3].Kind == StructureKind.Flat);

        Assert.That(intended, Is.Not.Null,
            "the zigzag-W2 / flat-W4 subdivision must be among the parsed counts");
        Assert.Multiple(() =>
        {
            Assert.That(intended!.Children[1].Children, Has.Count.EqualTo(3));
            Assert.That(intended.Children[1].End.Price, Is.EqualTo(112m));
            Assert.That(intended.Children[3].Children, Has.Count.EqualTo(3));
            Assert.That(intended.Children[3].End.Price, Is.EqualTo(143m));
        });
    }

    [Test]
    public void Parse_NestedImpulse_AssignsDegreesByDepth()
    {
        var result = WaveGrammarParser.Parse(NestedImpulse(), rootDegree: WaveDegree.Primary);

        var nested = result.Trees
            .Select(t => t.Root)
            .First(r => r.Kind == StructureKind.Impulse && r.Children[1].Kind == StructureKind.Zigzag);

        Assert.Multiple(() =>
        {
            Assert.That(nested.Degree, Is.EqualTo(WaveDegree.Primary));
            Assert.That(nested.Children[1].Degree, Is.EqualTo(WaveDegree.Intermediate));
            Assert.That(nested.Children[1].Children[0].Degree, Is.EqualTo(WaveDegree.Minor));
        });
    }

    [Test]
    public void Parse_BearishZigzag_TopCandidateIsZigzag()
    {
        // A=60 down, B retraces 60% to 176, C=56 down to 120 (≈ equality with A).
        var result = WaveGrammarParser.Parse(PivotsFromHigh(200m, 140m, 176m, 120m));

        Assert.That(result.Trees, Is.Not.Empty);
        var top = result.Trees[0].Root;
        Assert.Multiple(() =>
        {
            Assert.That(top.Kind, Is.EqualTo(StructureKind.Zigzag));
            Assert.That(top.RuleReport!.BullishAssumed, Is.False);
            Assert.That(top.Children.Select(c => c.Label), Is.EqualTo(new[] { "A", "B", "C" }));
        });
    }

    [Test]
    public void Parse_ContractingTriangle_IsAmongTheCandidates()
    {
        // Legs 30/18/14/10/7 — the triangle fixture from the rule checker tests.
        var result = WaveGrammarParser.Parse(Pivots(100m, 130m, 112m, 126m, 116m, 123m));

        Assert.That(
            result.Trees.Select(t => t.Root.Kind),
            Does.Contain(StructureKind.Triangle),
            "a clean contracting triangle must be parsed as such");
    }

    [Test]
    public void Parse_TooFewPivots_ReturnsEmpty()
    {
        // Three pivots span only two legs — below the smallest structure (a three).
        var result = WaveGrammarParser.Parse(Pivots(100m, 130m, 112m));

        Assert.That(result.Trees, Is.Empty);
    }

    [Test]
    public void Parse_DeepBWithShortC_IsARunningFlat_NotRejected()
    {
        // B beyond the origin (133% of A) and C short of A's end: hard rules hold — this is
        // a running flat, and the parser must find it rather than return nothing.
        var result = WaveGrammarParser.Parse(Pivots(100m, 130m, 90m, 95m));

        Assert.That(result.Trees.Select(t => t.Root.Kind), Does.Contain(StructureKind.Flat));
    }

    [Test]
    public void Parse_TreeNeverContainsHardRuleViolation()
    {
        var result = WaveGrammarParser.Parse(NestedImpulse());

        static void AssertNoHardFail(WaveNode node)
        {
            if (node.RuleReport is { } report)
            {
                Assert.That(
                    report.Rules.Any(r => r is { Status: RuleStatus.Fail, IsGuideline: false }),
                    Is.False,
                    $"node {node.Label} ({node.Kind}) carries a hard-rule violation");
            }
            foreach (var child in node.Children)
            {
                AssertNoHardFail(child);
            }
        }

        foreach (var tree in result.Trees)
        {
            AssertNoHardFail(tree.Root);
        }
    }

    [Test]
    public void Parse_IsDeterministic()
    {
        var first = WaveGrammarParser.Parse(NestedImpulse());
        var second = WaveGrammarParser.Parse(NestedImpulse());

        // Records holding lists have no deep equality — compare the serialized trees.
        var firstJson = System.Text.Json.JsonSerializer.Serialize(first);
        var secondJson = System.Text.Json.JsonSerializer.Serialize(second);
        Assert.That(secondJson, Is.EqualTo(firstJson));
    }

    [Test]
    public void Parse_EvaluationBudget_TruncatesInsteadOfHanging()
    {
        // A long, perfectly regular zigzag sea: combinatorially worst-case input.
        var prices = new List<decimal>();
        for (var i = 0; i < 60; i++)
        {
            prices.Add(i % 2 == 0 ? 100m + i : 140m + i);
        }
        var options = new WaveScoringOptions { MaxEvaluations = 500 };

        var result = WaveGrammarParser.Parse(Pivots([.. prices]), options);

        Assert.That(result.SearchTruncated, Is.True, "the tiny budget must trip the guard");
    }

    [Test]
    public void Parse_CancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => WaveGrammarParser.Parse(NestedImpulse(), cancellationToken: cts.Token));
    }

    [Test]
    public void Parse_ScoresAreWithinUnitInterval()
    {
        var result = WaveGrammarParser.Parse(NestedImpulse());

        Assert.That(result.Trees.Select(t => t.Score), Is.All.InRange(0m, 1m));
    }

    // ─── GenerateParsed mapping ────────────────────────────────────────────────

    [Test]
    public void GenerateParsed_MapsTreeToCandidateContract()
    {
        var (candidates, truncated) = WaveCandidateGenerator.GenerateParsed(NestedImpulse());

        Assert.That(candidates, Is.Not.Empty);
        var best = candidates[0];
        Assert.Multiple(() =>
        {
            Assert.That(truncated, Is.False);
            Assert.That(best.Id, Is.EqualTo(0));
            Assert.That(best.Structure, Is.EqualTo("Impulse"));
            Assert.That(best.Waves.Select(w => w.Label), Is.EqualTo(new[] { "1", "2", "3", "4", "5" }));
            Assert.That(best.Tree, Is.Not.Null);
            Assert.That(best.Score, Is.Not.Null);
            Assert.That(best.Levels, Is.Not.Null, "a motive root must carry forward levels");
        });
    }

    [Test]
    public void GenerateParsed_CorrectiveRoot_HasNoLevelsYet()
    {
        var (candidates, _) = WaveCandidateGenerator.GenerateParsed(
            PivotsFromHigh(200m, 140m, 176m, 120m));

        var zigzag = candidates.First(c => c.Structure == "Zigzag");
        Assert.That(zigzag.Levels, Is.Null,
            "corrective projections arrive with the corrective ProjectionService support");
    }
}
