using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// TDD for #224's momentum-divergence guideline (AC1, AC2, AC4): a 5-wave count whose wave-5 RSI
/// is weaker than wave-3's reports the confirming divergence as a guideline pass; missing RSI
/// (warm-up, or no indicator series supplied) is <see cref="RuleStatus.Indeterminate"/>, never a
/// guessed fail; every verdict is always a guideline (never invalidates the count).
/// </summary>
[TestFixture]
public sealed class MomentumDivergenceCheckerTests
{
    private static readonly DateTime Day = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // Origin + waves 1..5 — chronological, bullish (prices rising overall).
    private static List<WaveAnnotation> BullishImpulse() =>
    [
        new(Day, 100m, "1"),
        new(Day.AddDays(1), 120m, "1"),
        new(Day.AddDays(2), 110m, "2"),
        new(Day.AddDays(3), 150m, "3"),
        new(Day.AddDays(4), 130m, "4"),
        new(Day.AddDays(5), 170m, "5"),
    ];

    private static List<WaveAnnotation> BullishAbc() =>
    [
        new(Day, 100m, "O"),
        new(Day.AddDays(1), 80m, "A"),
        new(Day.AddDays(2), 90m, "B"),
        new(Day.AddDays(3), 70m, "C"),
    ];

    private static RsiResult Rsi(DateTime date, decimal? value) => new(date, value);

    [Test]
    public void Check_Impulse_Wave5RsiWeakerThanWave3_ReportsDivergencePresent_AC1()
    {
        var pivots = BullishImpulse();
        var rsi = new List<RsiResult>
        {
            Rsi(pivots[3].Date, 78m), // wave 3 — strong momentum
            Rsi(pivots[5].Date, 55m), // wave 5 — weaker despite the higher price
        };

        var result = MomentumDivergenceChecker.Check(pivots, rsi, macd: null);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(RuleStatus.Pass));
            Assert.That(result.IsGuideline, Is.True);
            Assert.That(result.Detail, Does.Contain("Momentum divergence present"));
        });
    }

    [Test]
    public void Check_Impulse_Wave5RsiNotWeaker_ReportsDivergenceAbsent_AsGuidelineFail()
    {
        var pivots = BullishImpulse();
        var rsi = new List<RsiResult>
        {
            Rsi(pivots[3].Date, 55m),
            Rsi(pivots[5].Date, 78m), // rising momentum into wave 5 — the non-confirming case
        };

        var result = MomentumDivergenceChecker.Check(pivots, rsi, macd: null);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(RuleStatus.Fail));
            Assert.That(result.IsGuideline, Is.True, "a failed guideline must never read as a hard-rule failure");
        });
    }

    [Test]
    public void Check_AbcCorrection_MirrorsTheImpulseCheckOnWaveAVsWaveC()
    {
        var pivots = BullishAbc(); // named for the fixture below, but O->A->B->C moves DOWN (a bearish correction)
        var rsi = new List<RsiResult>
        {
            Rsi(pivots[1].Date, 40m), // wave A — oversold, strong downside momentum
            Rsi(pivots[3].Date, 60m), // wave C — less oversold despite the lower price: confirming divergence
        };

        var result = MomentumDivergenceChecker.Check(pivots, rsi, macd: null);

        Assert.That(result.Status, Is.EqualTo(RuleStatus.Pass));
    }

    [Test]
    public void Check_NoRsiSeriesSupplied_IsIndeterminate_NeverAFail_AC2()
    {
        var result = MomentumDivergenceChecker.Check(BullishImpulse(), rsi: null, macd: null);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(RuleStatus.Indeterminate));
            Assert.That(result.IsGuideline, Is.True);
        });
    }

    [Test]
    public void Check_RsiMissingAtTheExactPivotDates_WarmUpPeriod_IsIndeterminate_AC2()
    {
        var pivots = BullishImpulse();
        // Series exists but has no entry landing on the wave-3/wave-5 dates (e.g. warm-up nulls).
        var rsi = new List<RsiResult> { Rsi(pivots[3].Date, null), Rsi(pivots[5].Date, null) };

        var result = MomentumDivergenceChecker.Check(pivots, rsi, macd: null);

        Assert.That(result.Status, Is.EqualTo(RuleStatus.Indeterminate));
    }

    [Test]
    public void Check_TooFewPivots_IsIndeterminate()
    {
        var pivots = BullishImpulse().Take(3).ToList(); // only through wave 2
        var result = MomentumDivergenceChecker.Check(pivots, rsi: [], macd: null);

        Assert.That(result.Status, Is.EqualTo(RuleStatus.Indeterminate));
    }
}
