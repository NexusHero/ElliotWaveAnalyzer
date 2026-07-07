using System.Text.Json;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Pins AC4 (#188): computing the context overlay can never alter a count's geometry. Structurally
/// guaranteed — <see cref="CatalystWindowFlagger"/> and <see cref="IntermarketDivergenceDetector"/>
/// never take a <see cref="WaveLevels"/> (or any count object) as input, only bare dates/booleans/
/// numbers — so there is no reference through which they could mutate one. This test pins that
/// invariant against a live <see cref="WaveLevels"/> fixture, the way <see cref="MoodDivergenceDetector"/>'s
/// tests pin "never touches WaveLevels" for #183.
/// </summary>
[TestFixture]
public sealed class ContextNonInterferenceTests
{
    [Test]
    public void ComputingTheContextOverlay_NeverMutatesAWaveLevelsFixture()
    {
        var levels = new WaveLevels(
            "Wave 5", Bullish: true,
            Invalidation: new PriceLevel(120.00m, LevelSide.Below, "inv", "end of 1"),
            SupportZone: new PriceZone(130.00m, 135.00m, "entry", "fib"),
            TargetZones: [new PriceZone(180.00m, 190.00m, "target", "ext")],
            Alternative: null);
        var before = JsonSerializer.Serialize(levels);

        _ = CatalystWindowFlagger.Flag(
            [new CatalystEvent(new DateTime(2026, 3, 15), "FOMC", "TestCalendar")],
            [new DateTime(2026, 3, 16)], windowDays: 3);
        _ = IntermarketDivergenceDetector.Detect(
            levels.Bullish, [new IntermarketReading("DXY", -0.6, -0.01m)]);

        Assert.That(JsonSerializer.Serialize(levels), Is.EqualTo(before));
    }
}
