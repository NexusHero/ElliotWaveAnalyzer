using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// The hallucination guard: a claimed pivot within tolerance of a real candle extreme snaps to that
/// exact candle; one outside tolerance is rejected with the reason shape the report surfaces.
/// </summary>
[TestFixture]
public sealed class PivotSnapperTests
{
    private static readonly DateTime T0 = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly IReadOnlyList<MarketCandle> Candles =
    [
        new(T0, 100m, 105m, 99m, 104m, 0m),
        new(T0.AddDays(1), 104m, 112m, 103m, 110m, 0m),
        new(T0.AddDays(2), 110m, 111m, 100m, 101m, 0m),
    ];

    [Test]
    public void Snap_ClaimWithinTolerance_SnapsToTheExactCandleExtreme()
    {
        // Claimed 112.2 on day 1 is within 0.5% of the candle-1 high (112) → snaps to (day1, 112).
        var claimed = new[] { new ClaimedPivot(T0.AddDays(1), 112.2m, "3") };

        var (snapped, rejected) = PivotSnapper.Snap(claimed, Candles);

        Assert.Multiple(() =>
        {
            Assert.That(rejected, Is.Empty);
            Assert.That(snapped, Has.Count.EqualTo(1));
            Assert.That(snapped[0].Date, Is.EqualTo(T0.AddDays(1)));
            Assert.That(snapped[0].Price, Is.EqualTo(112m));
            Assert.That(snapped[0].ClaimedPrice, Is.EqualTo(112.2m));
        });
    }

    [Test]
    public void Snap_ClaimOutsideTolerance_IsRejectedWithReason()
    {
        // 64.86 is nowhere near any extreme → rejected with the documented reason shape.
        var claimed = new[] { new ClaimedPivot(T0.AddDays(1), 64.86m, "3") };

        var (snapped, rejected) = PivotSnapper.Snap(claimed, Candles);

        Assert.Multiple(() =>
        {
            Assert.That(snapped, Is.Empty);
            Assert.That(rejected, Has.Count.EqualTo(1));
            Assert.That(rejected[0].Reason, Does.Contain("no such extreme within ±0.5%"));
            Assert.That(rejected[0].Reason, Does.Contain("64.86"));
        });
    }

    [Test]
    public void Snap_ClaimOffByMoreThanOneBar_IsRejected()
    {
        // A 10-day flat series with a lone 112 high on day 1; a claim of 112 on day 5 is >1 bar from
        // that candle, so the nearest-by-date window (days 4–6) never sees it → rejected.
        var candles = Enumerable.Range(0, 10)
            .Select(i => new MarketCandle(T0.AddDays(i), 100m, i == 1 ? 112m : 101m, 99m, 100m, 0m))
            .ToList();
        var claimed = new[] { new ClaimedPivot(T0.AddDays(5), 112m, "3") };

        var (snapped, rejected) = PivotSnapper.Snap(claimed, candles);

        Assert.Multiple(() =>
        {
            Assert.That(snapped, Is.Empty);
            Assert.That(rejected, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Snap_SnapsToLowExtreme_WhenTheClaimIsNearACandleLow()
    {
        var claimed = new[] { new ClaimedPivot(T0.AddDays(2), 100.1m, "4") };

        var (snapped, _) = PivotSnapper.Snap(claimed, Candles);

        Assert.That(snapped[0].Price, Is.EqualTo(100m));
    }
}
