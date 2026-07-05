using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// The deterministic verification: too few snapped pivots → unreliable (no fabricated verdicts); a
/// clean claimed impulse passes the hard rules; a wave-2 violation is reported by name; and the report
/// carries a side-by-side comparison with our own count.
/// </summary>
[TestFixture]
public sealed class ChartVerificationAssemblerTests
{
    private static readonly DateTime T0 = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Builds one candle per pivot (its extreme = the pivot price) and matching claimed pivots.</summary>
    private static (ChartExtraction Extraction, IReadOnlyList<MarketCandle> Candles) Build(
        params (int Day, decimal Price, bool IsHigh, string Label)[] pivots)
    {
        var candles = new List<MarketCandle>();
        var claimed = new List<ClaimedPivot>();
        foreach (var (day, price, isHigh, label) in pivots)
        {
            var date = T0.AddDays(day);
            var high = isHigh ? price : price * 1.01m;
            var low = isHigh ? price * 0.99m : price;
            candles.Add(new MarketCandle(date, price, high, low, price, 0m));
            claimed.Add(new ClaimedPivot(date, price, label));
        }

        return (new ChartExtraction("ACME", "1D", claimed, [], []), candles);
    }

    [Test]
    public void Assemble_CleanImpulse_PassesTheHardRules()
    {
        // 0-1-2-3-4-5: valid impulse (wave2 holds, wave3 longest, wave4 no overlap).
        var (extraction, candles) = Build(
            (0, 100m, false, "0"), (10, 130m, true, "1"), (20, 115m, false, "2"),
            (30, 175m, true, "3"), (40, 150m, false, "4"), (50, 200m, true, "5"));

        var report = ChartVerificationAssembler.Assemble(extraction, candles);

        Assert.Multiple(() =>
        {
            Assert.That(report.Status, Is.EqualTo(ImageVerificationStatus.Verified));
            Assert.That(report.Snapped, Has.Count.EqualTo(6));
            Assert.That(report.ClaimedRules!.Rules.Any(r => r.Status == RuleStatus.Fail && !r.IsGuideline), Is.False);
            Assert.That(report.Comparison, Is.Not.Null);
        });
    }

    [Test]
    public void Assemble_Wave2Violation_ReportsThatRuleFailedByName()
    {
        // Wave 2 (price 95) retraces below the origin (100) → Rule 1 fails.
        var (extraction, candles) = Build(
            (0, 100m, false, "0"), (10, 130m, true, "1"), (20, 95m, false, "2"),
            (30, 175m, true, "3"), (40, 150m, false, "4"), (50, 200m, true, "5"));

        var report = ChartVerificationAssembler.Assemble(extraction, candles);

        var failed = report.ClaimedRules!.Rules.Where(r => r.Status == RuleStatus.Fail).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(report.Status, Is.EqualTo(ImageVerificationStatus.Verified));
            Assert.That(failed, Is.Not.Empty);
            Assert.That(failed.Any(r => r.Name.Contains("Wave 2", StringComparison.Ordinal)), Is.True);
        });
    }

    [Test]
    public void Assemble_TooFewPivotsSnap_IsExtractionUnreliable_NoVerdicts()
    {
        // Labels imply a 5-wave (needs 6 pivots) but only 3 are claimed → unreliable, no rule verdicts.
        var (extraction, candles) = Build(
            (0, 100m, false, "0"), (10, 130m, true, "1"), (50, 200m, true, "5"));

        var report = ChartVerificationAssembler.Assemble(extraction, candles);

        Assert.Multiple(() =>
        {
            Assert.That(report.Status, Is.EqualTo(ImageVerificationStatus.ExtractionUnreliable));
            Assert.That(report.ClaimedRules, Is.Null);
            Assert.That(report.Comparison, Is.Null);
            Assert.That(report.Message, Does.Contain("could not be reliably extracted"));
        });
    }

    [Test]
    public void Assemble_HallucinatedPivot_IsRejectedAndCounted()
    {
        // Five real pivots plus one whose price matches no candle extreme → rejected, leaving 5 < 6.
        var (extraction, candles) = Build(
            (0, 100m, false, "0"), (10, 130m, true, "1"), (20, 115m, false, "2"),
            (30, 175m, true, "3"), (40, 150m, false, "4"));
        var withGhost = extraction with
        {
            Pivots = [.. extraction.Pivots, new ClaimedPivot(T0.AddDays(50), 9_999m, "5")],
        };

        var report = ChartVerificationAssembler.Assemble(withGhost, candles);

        Assert.Multiple(() =>
        {
            Assert.That(report.Rejected, Has.Count.EqualTo(1));
            Assert.That(report.Rejected[0].Label, Is.EqualTo("5"));
            Assert.That(report.Status, Is.EqualTo(ImageVerificationStatus.ExtractionUnreliable));
        });
    }
}
