using System.Text.Json;
using CsCheck;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Properties;

/// <summary>
/// Property-based invariants for <see cref="PivotSnapper"/>: snapping is idempotent (snapping an
/// already-snapped pivot is a no-op), and every snapped pivot lands on a real candle extreme.
/// </summary>
[TestFixture]
public sealed class PivotSnapperProperties
{
    [Test]
    public void Snap_IsIdempotent()
    {
        PropertyGenerators.Scenarios.Sample(s =>
        {
            var claimed = s.Annotations.Select(a => new ClaimedPivot(a.Date, a.Price, a.Label)).ToList();
            var (snappedOnce, _) = PivotSnapper.Snap(claimed, s.Candles);

            var reclaimed = snappedOnce.Select(p => new ClaimedPivot(p.Date, p.Price, p.Label)).ToList();
            var (snappedTwice, _) = PivotSnapper.Snap(reclaimed, s.Candles);

            Assert.That(JsonSerializer.Serialize(snappedTwice), Is.EqualTo(JsonSerializer.Serialize(snappedOnce)));
        });
    }

    [Test]
    public void Snap_OnlyEverLandsOnRealCandleExtremes()
    {
        PropertyGenerators.Scenarios.Sample(s =>
        {
            var claimed = s.Annotations.Select(a => new ClaimedPivot(a.Date, a.Price, a.Label)).ToList();
            var (snapped, _) = PivotSnapper.Snap(claimed, s.Candles);

            var extremes = s.Candles.SelectMany(c => new[] { c.High, c.Low }).ToHashSet();
            foreach (var pivot in snapped)
            {
                Assert.That(extremes.Contains(pivot.Price), Is.True);
            }
        });
    }
}
