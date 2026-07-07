using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// <see cref="IntermarketDivergenceDetector"/>: classifies a related instrument's own move as
/// supporting or contradicting a count's thesis (#188, AC3).
/// </summary>
[TestFixture]
public sealed class IntermarketDivergenceDetectorTests
{
    [Test]
    public void Detect_PositivelyCorrelatedInstrumentMovingWithTheThesis_IsSupport()
    {
        // Bullish thesis; a positively correlated peer is also up — corroborates.
        var reading = new IntermarketReading("PEER", Correlation: 0.75, PercentChange: 0.02m);

        var signals = IntermarketDivergenceDetector.Detect(thesisBullish: true, [reading]);

        Assert.That(signals[0].Kind, Is.EqualTo(IntermarketSignalKind.Support));
    }

    [Test]
    public void Detect_PositivelyCorrelatedInstrumentMovingAgainstTheThesis_IsContradiction()
    {
        // Bullish thesis; a positively correlated peer is down — contradicts.
        var reading = new IntermarketReading("PEER", Correlation: 0.75, PercentChange: -0.02m);

        var signals = IntermarketDivergenceDetector.Detect(thesisBullish: true, [reading]);

        Assert.That(signals[0].Kind, Is.EqualTo(IntermarketSignalKind.Contradiction));
    }

    [Test]
    public void Detect_NegativelyCorrelatedInstrumentMovingOppositeTheThesis_IsSupport()
    {
        // Bullish equity thesis; DXY (negatively correlated) falling — corroborates a risk-on move.
        var dxy = new IntermarketReading("DXY", Correlation: -0.6, PercentChange: -0.01m);

        var signals = IntermarketDivergenceDetector.Detect(thesisBullish: true, [dxy]);

        Assert.That(signals[0].Kind, Is.EqualTo(IntermarketSignalKind.Support));
    }

    [Test]
    public void Detect_NegativelyCorrelatedInstrumentMovingWithTheThesisDirection_IsContradiction()
    {
        // Bullish equity thesis; DXY (negatively correlated) also rising — a real cross-market conflict.
        var dxy = new IntermarketReading("DXY", Correlation: -0.6, PercentChange: 0.01m);

        var signals = IntermarketDivergenceDetector.Detect(thesisBullish: true, [dxy]);

        Assert.That(signals[0].Kind, Is.EqualTo(IntermarketSignalKind.Contradiction));
    }

    [Test]
    public void Detect_WeakCorrelation_IsExcludedNotForceClassified()
    {
        var weak = new IntermarketReading("RANDOM", Correlation: 0.05, PercentChange: 0.05m);

        var signals = IntermarketDivergenceDetector.Detect(thesisBullish: true, [weak]);

        Assert.That(signals, Is.Empty);
    }

    [Test]
    public void Detect_CarriesTheRawCorrelationAndPercentChangeThroughUnchanged()
    {
        var reading = new IntermarketReading("PEER", Correlation: 0.42, PercentChange: 0.0137m);

        var signals = IntermarketDivergenceDetector.Detect(thesisBullish: true, [reading]);

        Assert.Multiple(() =>
        {
            Assert.That(signals[0].Correlation, Is.EqualTo(0.42));
            Assert.That(signals[0].PercentChange, Is.EqualTo(0.0137m));
        });
    }

    [Test]
    public void Detect_NullReadings_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => IntermarketDivergenceDetector.Detect(true, null!));
    }
}
