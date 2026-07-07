using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// <see cref="WaveVerificationService"/> must verify against the SAME series the chart displayed:
/// a pivot placed on a weekly bar carries the week-start date and the weekly extreme, which no
/// single daily candle matches (the extreme prints mid-week) — so daily-only snapping wrongly
/// rejected every weekly count. Verifying on the interval-resampled series fixes that.
/// </summary>
[TestFixture]
public sealed class WaveVerificationServiceTests
{
    // Monday 2024-01-01; three trading weeks of dailies where each week's extreme prints Thursday.
    private static readonly DateTime Mon1 = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static IReadOnlyList<MarketCandle> ThreeWeeksDaily()
    {
        var candles = new List<MarketCandle>();
        decimal[] weekHighs = [110m, 130m, 120m];
        for (var w = 0; w < 3; w++)
        {
            var monday = Mon1.AddDays(7 * w);
            for (var d = 0; d < 5; d++)
            {
                var mid = 100m + (w * 10m);
                // Thursday (d == 3) prints the week's extreme — far from the Monday bar.
                var high = d == 3 ? weekHighs[w] : mid + 1m;
                var low = d == 3 ? mid - 6m : mid - 1m;
                candles.Add(new MarketCandle(monday.AddDays(d), mid, high, low, mid, 100m));
            }
        }

        return candles;
    }

    private sealed class StubDaily(IReadOnlyList<MarketCandle> candles) : IMarketDataProvider
    {
        public bool Supports(string symbol) => true;

        public Task<IReadOnlyList<MarketCandle>> GetCandlesAsync(
            string symbol, int days, CancellationToken cancellationToken = default)
            => Task.FromResult(candles);
    }

    [Test]
    public async Task VerifyAsync_WeeklyInterval_SnapsPivotsPlacedOnWeeklyBars()
    {
        var service = new WaveVerificationService([new StubDaily(ThreeWeeksDaily())], []);
        // Pivots as the weekly chart shows them: week-start date, weekly extreme price.
        var annotations = new List<WaveAnnotation>
        {
            new(Mon1, 110m, "1"),
            new(Mon1.AddDays(7), 130m, "2"),
        };

        var weekly = await service.VerifyAsync("SPX", annotations, 730, CandleInterval.OneWeek);

        Assert.Multiple(() =>
        {
            Assert.That(weekly.Rejected, Is.Empty, "weekly pivots must snap on the weekly series");
            Assert.That(weekly.Snapped, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task VerifyAsync_DailyInterval_RejectsWeeklyPlacedPivots_TheBugThisGuards()
    {
        var service = new WaveVerificationService([new StubDaily(ThreeWeeksDaily())], []);
        var annotations = new List<WaveAnnotation>
        {
            new(Mon1, 110m, "1"),
            new(Mon1.AddDays(7), 130m, "2"),
        };

        // Against raw dailies the Monday bars don't carry the weekly extreme (±1-bar tolerance
        // can't reach Thursday) — exactly why the interval must ride with the request.
        var daily = await service.VerifyAsync("SPX", annotations, 730, CandleInterval.OneDay);

        Assert.That(daily.Rejected, Is.Not.Empty);
    }
}
