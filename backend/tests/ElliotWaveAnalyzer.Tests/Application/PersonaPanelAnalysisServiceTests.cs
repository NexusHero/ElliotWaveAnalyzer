using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Orchestration of #184's persona panel: candidates are generated once, deterministically, before
/// any persona runs (AC1 — the panel cannot influence which counts exist); the panel's per-persona
/// picks are joined back onto that same candidate geometry (endorsing personas surfaced honestly);
/// and no candidates means no LLM call at all (mirrors <c>AutoWaveAnalysisService</c>).
/// </summary>
[TestFixture]
public sealed class PersonaPanelAnalysisServiceTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private sealed class FakeProvider(IReadOnlyList<MarketCandle> candles) : IMarketDataProvider
    {
        public bool Supports(string symbol) => true;

        public Task<IReadOnlyList<MarketCandle>> GetCandlesAsync(
            string symbol, int days, CancellationToken cancellationToken = default) => Task.FromResult(candles);
    }

    private sealed class FakeTokenTracker : ITokenTracker
    {
        public bool BudgetExceeded { get; set; }
        public TokenUsage? Recorded { get; private set; }

        public void Record(TokenUsage usage) => Recorded = usage;
        public TokenUsageReport GetReport() => new(0, 0, 0, null, false, new Dictionary<string, int>());
        public bool IsBudgetExceeded() => BudgetExceeded;
    }

    private sealed class FakePanel : IPersonaAnalystPanel
    {
        public Func<IReadOnlyList<WaveCandidate>, PersonaPanelRankResult>? OnRank { get; set; }
        public bool Called { get; private set; }

        public Task<PersonaPanelRankResult> RankAsync(
            Guid userId, string symbol, IReadOnlyList<MarketCandle> candles, IReadOnlyList<WaveCandidate> candidates,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            return Task.FromResult(OnRank!(candidates));
        }
    }

    // A repeating up-impulse path, enough clean swings for the detector to land several pivots
    // (same technique AlternateHypothesisServiceTests uses).
    private static IReadOnlyList<MarketCandle> Series()
    {
        decimal[] block = [100, 130, 115, 175, 150, 200];
        var turns = new List<decimal>();
        decimal lift = 0m;
        for (var b = 0; b < 4; b++)
        {
            foreach (var p in block)
            {
                turns.Add(p + lift);
            }

            lift += 120m;
        }

        var candles = new List<MarketCandle>();
        var day = 0;
        for (var i = 0; i + 1 < turns.Count; i++)
        {
            for (var s = 0; s < 6; s++)
            {
                var t = (decimal)(s + 1) / 6;
                var price = turns[i] + ((turns[i + 1] - turns[i]) * t);
                var prev = candles.Count > 0 ? candles[^1].Close : turns[0];
                candles.Add(new MarketCandle(
                    Start.AddDays(day++), prev, Math.Max(prev, price), Math.Min(prev, price), price, 0m));
            }
        }

        return candles;
    }

    private static PersonaRanking Ranking(string persona, int bestId) =>
        new(persona, new AutoWaveRanking(bestId, $"{persona} read",
            [new RankedCandidate(bestId, "high", $"{persona} rationale", $"{persona} outlook")]));

    [Test]
    public async Task NoRuleValidCandidates_ReturnsEmptyResponse_WithoutCallingThePanel()
    {
        // A flat/empty series generates no swing pivots and therefore no candidates.
        var flat = Enumerable.Range(0, 10)
            .Select(i => new MarketCandle(Start.AddDays(i), 100m, 100m, 100m, 100m, 0m))
            .ToList();
        var panel = new FakePanel();
        var service = new PersonaPanelAnalysisService(
            [new FakeProvider(flat)], panel, new FakeTokenTracker(),
            NSubstitute.Substitute.For<IIndicatorCalculator>(), NullLogger<PersonaPanelAnalysisService>.Instance);

        var result = await service.AnalyzeAsync(Guid.NewGuid(), "BTC", 365, 2.5m);

        Assert.Multiple(() =>
        {
            Assert.That(result.Rankings, Is.Empty);
            Assert.That(result.PersonasAttempted, Is.EqualTo(0));
            Assert.That(panel.Called, Is.False);
        });
    }

    [Test]
    public async Task WithCandidates_MergesPersonaVotesOntoDeterministicGeometry_AndTagsEndorsingPersonas()
    {
        var candles = Series();
        var panel = new FakePanel
        {
            OnRank = candidates =>
            {
                var winner = candidates[0].Id;
                var rankings = new List<PersonaRanking>
                {
                    Ranking("conservative", winner),
                    Ranking("aggressive", winner),
                    Ranking("contrarian", candidates.Count > 1 ? candidates[1].Id : winner),
                };
                var weights = rankings.Select(r => new PersonaWeight(r.Persona, 0.5, IsNeutralPrior: true)).ToList();
                return new PersonaPanelRankResult(rankings, weights, new TokenUsage("test", 30, 15, 45), 3);
            },
        };
        var service = new PersonaPanelAnalysisService(
            [new FakeProvider(candles)], panel, new FakeTokenTracker(),
            NSubstitute.Substitute.For<IIndicatorCalculator>(), NullLogger<PersonaPanelAnalysisService>.Instance);

        var result = await service.AnalyzeAsync(Guid.NewGuid(), "BTC", 365, 2.5m);

        Assert.Multiple(() =>
        {
            Assert.That(panel.Called, Is.True);
            Assert.That(result.Rankings, Is.Not.Empty);
            Assert.That(result.PersonasAttempted, Is.EqualTo(3));
            Assert.That(result.Weights, Has.Count.EqualTo(3));

            // Every rule-valid candidate the panel returned still carries its own deterministic
            // geometry — the panel could only rank/explain, never invent one (AC1).
            Assert.That(result.Rankings.All(r => r.RuleReport is not null), Is.True);

            // Two of three personas agreed on the winner — that agreement is surfaced, not hidden.
            var best = result.Rankings.Single(r => r.IsBest);
            Assert.That(best.EndorsingPersonas, Has.Count.GreaterThanOrEqualTo(1));
        });
    }
}
