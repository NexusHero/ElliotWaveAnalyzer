using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Unit tests for the pure <see cref="AlertDecision"/>: an alert fires exactly once, on the
/// transition from a still-pending analysis to a terminal outcome.
/// </summary>
[TestFixture]
public sealed class AlertDecisionTests
{
    [Test]
    public void PendingToInvalidated_AlertsWithInvalidated()
    {
        Assert.That(
            AlertDecision.NewAlert(AnalysisOutcome.Pending, AnalysisOutcome.Invalidated),
            Is.EqualTo(AnalysisOutcome.Invalidated));
    }

    [Test]
    public void PendingToTargetReached_AlertsWithTargetReached()
    {
        Assert.That(
            AlertDecision.NewAlert(AnalysisOutcome.Pending, AnalysisOutcome.TargetReached),
            Is.EqualTo(AnalysisOutcome.TargetReached));
    }

    [Test]
    public void StillPending_DoesNotAlert()
    {
        Assert.That(
            AlertDecision.NewAlert(AnalysisOutcome.Pending, AnalysisOutcome.Pending),
            Is.Null);
    }

    [Test]
    [TestCase(AnalysisOutcome.Invalidated)]
    [TestCase(AnalysisOutcome.TargetReached)]
    public void AlreadyAlerted_NeverAlertsAgain(AnalysisOutcome current)
    {
        // Whatever the fresh evaluation says, a snapshot that already alerted is settled.
        Assert.Multiple(() =>
        {
            Assert.That(AlertDecision.NewAlert(AnalysisOutcome.Invalidated, current), Is.Null);
            Assert.That(AlertDecision.NewAlert(AnalysisOutcome.TargetReached, current), Is.Null);
        });
    }
}
