using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Orchestration of #186: out-of-vocabulary proposals are dropped before generation (AC1), the count
/// tested is capped (AC5), and with no LLM configured the feature is off (AC6). Every in-vocabulary
/// proposal is turned into a deterministic verdict — the LLM never decides validity.
/// </summary>
[TestFixture]
public sealed class AlternateHypothesisServiceTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static AlternateHypothesisService Service(FakeProposer proposer) =>
        new([new FakeProvider()], proposer, NullLogger<AlternateHypothesisService>.Instance);

    [Test]
    public async Task AnalyzeAsync_NoLlmConfigured_ReportsUnavailable()
    {
        var report = await Service(new FakeProposer { Configured = false }).AnalyzeAsync("BTC", CandleInterval.OneDay);

        Assert.Multiple(() =>
        {
            Assert.That(report.Unavailable, Is.Not.Null);
            Assert.That(report.Validated, Is.Empty);
            Assert.That(report.Rejected, Is.Empty);
        });
    }

    [Test]
    public async Task AnalyzeAsync_DropsOutOfVocabularyProposalsBeforeGeneration()
    {
        var proposer = new FakeProposer
        {
            Proposals =
            [
                new RawHypothesis("combination", "made up"),
                new RawHypothesis("impulse", "clean five"),
            ],
        };

        var report = await Service(proposer).AnalyzeAsync("BTC", CandleInterval.OneDay);

        // Only the in-vocabulary "impulse" produced a verdict; "combination" never reached the engine.
        var total = report.Validated.Count + report.Rejected.Count;
        Assert.Multiple(() =>
        {
            Assert.That(total, Is.EqualTo(1));
            Assert.That(report.Validated.Concat(report.Rejected).All(h => h.Structure == "Impulse"), Is.True);
        });
    }

    [Test]
    public async Task AnalyzeAsync_CapsTheNumberOfProposalsTested()
    {
        var proposer = new FakeProposer
        {
            Proposals = Enumerable.Range(0, 9).Select(_ => new RawHypothesis("flat", "x")).ToList(),
        };

        var report = await Service(proposer).AnalyzeAsync("BTC", CandleInterval.OneDay);

        Assert.That(report.ProposalCapHit, Is.True);
    }

    private sealed class FakeProposer : IHypothesisProposer
    {
        public bool Configured { get; init; } = true;
        public IReadOnlyList<RawHypothesis> Proposals { get; init; } = [];
        public bool IsConfigured => Configured;

        public Task<IReadOnlyList<RawHypothesis>> ProposeAsync(
            string symbol, IReadOnlyList<SwingPivot> pivots, int max, CancellationToken cancellationToken = default)
            => Task.FromResult(Proposals);
    }

    private sealed class FakeProvider : IMarketDataProvider
    {
        public bool Supports(string symbol) => true;

        public Task<IReadOnlyList<MarketCandle>> GetCandlesAsync(
            string symbol, int days, CancellationToken cancellationToken = default)
            => Task.FromResult(Series());

        // A repeating up-impulse path, enough clean swings for the detector to land several pivots.
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
    }
}
