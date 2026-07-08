using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// TDD for #224's volume guideline (AC2, AC4): wave 3 volume exceeding wave 5's is the textbook
/// impulse signature; wave B volume contracting against both A and C is the textbook correction
/// signature. All-zero volume in a checked window (a provider that doesn't report it) is
/// <see cref="RuleStatus.Indeterminate"/>, never a guessed fail — every verdict is a guideline.
/// </summary>
[TestFixture]
public sealed class VolumeGuidelineCheckerTests
{
    private static readonly DateTime Day = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static List<WaveAnnotation> Impulse() =>
    [
        new(Day, 100m, "1"),
        new(Day.AddDays(1), 120m, "1"),
        new(Day.AddDays(2), 110m, "2"),
        new(Day.AddDays(3), 150m, "3"),
        new(Day.AddDays(4), 130m, "4"),
        new(Day.AddDays(5), 170m, "5"),
    ];

    private static List<WaveAnnotation> Abc() =>
    [
        new(Day, 100m, "O"),
        new(Day.AddDays(1), 120m, "A"),
        new(Day.AddDays(2), 110m, "B"),
        new(Day.AddDays(3), 130m, "C"),
    ];

    private static MarketCandle Candle(DateTime date, decimal volume) => new(date, 100m, 101m, 99m, 100m, volume);

    [Test]
    public void Check_Impulse_Wave3VolumeExceedsWave5_ReportsConfirmed_AC1()
    {
        var pivots = Impulse();
        var candles = new List<MarketCandle>
        {
            Candle(pivots[3].Date, 1000m), // inside wave 3's window (after wave-2 end, at wave-3 end)
            Candle(pivots[5].Date, 300m), // inside wave 5's window
        };

        var result = VolumeGuidelineChecker.Check(pivots, candles);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(RuleStatus.Pass));
            Assert.That(result.IsGuideline, Is.True);
            Assert.That(result.Detail, Does.Contain("textbook signature"));
        });
    }

    [Test]
    public void Check_Impulse_Wave5VolumeExceedsWave3_ReportsGuidelineFail()
    {
        var pivots = Impulse();
        var candles = new List<MarketCandle>
        {
            Candle(pivots[3].Date, 200m),
            Candle(pivots[5].Date, 900m),
        };

        var result = VolumeGuidelineChecker.Check(pivots, candles);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(RuleStatus.Fail));
            Assert.That(result.IsGuideline, Is.True);
        });
    }

    [Test]
    public void Check_AbcCorrection_WaveBContractsAgainstAAndC_ReportsConfirmed()
    {
        var pivots = Abc();
        var candles = new List<MarketCandle>
        {
            Candle(pivots[1].Date, 800m), // wave A
            Candle(pivots[2].Date, 200m), // wave B — anemic
            Candle(pivots[3].Date, 700m), // wave C
        };

        var result = VolumeGuidelineChecker.Check(pivots, candles);

        Assert.That(result.Status, Is.EqualTo(RuleStatus.Pass));
    }

    [Test]
    public void Check_AllCandlesReportZeroVolume_IsIndeterminate_NeverAFail_AC2()
    {
        var pivots = Impulse();
        var candles = pivots.Select(p => Candle(p.Date, 0m)).ToList();

        var result = VolumeGuidelineChecker.Check(pivots, candles);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(RuleStatus.Indeterminate));
            Assert.That(result.Detail, Does.Contain("does not report volume"));
        });
    }

    [Test]
    public void Check_NoCandlesInAWavesWindow_IsIndeterminate()
    {
        var pivots = Impulse();
        // Candles exist, but none fall inside the wave-3/wave-5 windows.
        var candles = new List<MarketCandle> { Candle(Day.AddYears(1), 500m) };

        var result = VolumeGuidelineChecker.Check(pivots, candles);

        Assert.That(result.Status, Is.EqualTo(RuleStatus.Indeterminate));
    }

    [Test]
    public void Check_TooFewPivots_IsIndeterminate()
    {
        var pivots = Impulse().Take(3).ToList();
        var result = VolumeGuidelineChecker.Check(pivots, []);

        Assert.That(result.Status, Is.EqualTo(RuleStatus.Indeterminate));
    }
}
