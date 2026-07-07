using ElliotWaveAnalyzer.Api.Application;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// <see cref="SustainedFailureDetector"/>: a single failure never alerts; a sustained streak
/// alerts exactly once per episode; a healthy result resets the streak so the same problem
/// recurring later alerts again (#173 AC3).
/// </summary>
[TestFixture]
public sealed class SustainedFailureDetectorTests
{
    [Test]
    public void RecordAndShouldAlert_BelowThreshold_NeverAlerts()
    {
        var sut = new SustainedFailureDetector(consecutiveFailureThreshold: 3);

        Assert.Multiple(() =>
        {
            Assert.That(sut.RecordAndShouldAlert(false), Is.False);
            Assert.That(sut.RecordAndShouldAlert(false), Is.False);
        });
    }

    [Test]
    public void RecordAndShouldAlert_AtThreshold_AlertsExactlyOnce()
    {
        var sut = new SustainedFailureDetector(consecutiveFailureThreshold: 3);
        sut.RecordAndShouldAlert(false);
        sut.RecordAndShouldAlert(false);

        var atThreshold = sut.RecordAndShouldAlert(false);
        var stillFailing = sut.RecordAndShouldAlert(false);

        Assert.Multiple(() =>
        {
            Assert.That(atThreshold, Is.True);
            Assert.That(stillFailing, Is.False, "must not re-alert every poll while still failing");
        });
    }

    [Test]
    public void RecordAndShouldAlert_HealthyResetsTheStreak_SoARecurrenceAlertsAgain()
    {
        var sut = new SustainedFailureDetector(consecutiveFailureThreshold: 2);
        sut.RecordAndShouldAlert(false);
        var firstAlert = sut.RecordAndShouldAlert(false);

        sut.RecordAndShouldAlert(true); // recovered

        sut.RecordAndShouldAlert(false);
        var secondAlert = sut.RecordAndShouldAlert(false);

        Assert.Multiple(() =>
        {
            Assert.That(firstAlert, Is.True);
            Assert.That(secondAlert, Is.True, "a fresh sustained failure after recovery must alert again");
        });
    }

    [Test]
    public void RecordAndShouldAlert_HealthyBeforeAnyFailure_NeverAlerts()
    {
        var sut = new SustainedFailureDetector(consecutiveFailureThreshold: 1);

        Assert.That(sut.RecordAndShouldAlert(true), Is.False);
    }
}
