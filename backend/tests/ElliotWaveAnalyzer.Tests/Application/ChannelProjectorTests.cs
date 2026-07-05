using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Unit tests for the pure <see cref="ChannelProjector"/>: base- and acceleration-channel line
/// equations (slope/intercept) in linear and log space against hand-computed values, and the
/// projected wave-5 target band. Pivots are hand-built so every number is computable by hand.
/// </summary>
[TestFixture]
public sealed class ChannelProjectorTests
{
    private static readonly DateTime T0 = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // A clean impulse: P0..P4 ten days apart. Days from origin: 0,10,20,30,40.
    private static IReadOnlyList<WaveAnnotation> Impulse() =>
    [
        new(T0, 100m, "0"),
        new(T0.AddDays(10), 160m, "1"),
        new(T0.AddDays(20), 130m, "2"),
        new(T0.AddDays(30), 250m, "3"),
        new(T0.AddDays(40), 210m, "4"),
    ];

    [Test]
    public void Project_BaseChannel_Linear_MatchesHandComputedSlopeAndIntercept()
    {
        // Baseline 0→2: (130-100)/(20-0) = 1.5, intercept 100. Parallel through 1: 160 - 1.5·10 = 145.
        var channels = ChannelProjector.Project(Impulse(), FibScale.Linear);
        var baseChannel = channels.Single(c => c.Kind == ChannelKind.Base);

        Assert.Multiple(() =>
        {
            Assert.That(baseChannel.Baseline.Slope, Is.EqualTo(1.5m).Within(1e-6m));
            Assert.That(baseChannel.Baseline.Intercept, Is.EqualTo(100m).Within(1e-6m));
            Assert.That(baseChannel.Parallel.Slope, Is.EqualTo(1.5m).Within(1e-6m));
            Assert.That(baseChannel.Parallel.Intercept, Is.EqualTo(145m).Within(1e-6m));
        });
    }

    [Test]
    public void Project_BaseChannel_Log_MatchesHandComputedLnSpaceEquation()
    {
        // ln space: slope = (ln130 - ln100)/20 = 0.013118213…; intercept = ln100 = 4.605170186…
        // parallel through 1: ln160 - slope·10 = 4.943991683…
        var channels = ChannelProjector.Project(Impulse(), FibScale.Log);
        var baseChannel = channels.Single(c => c.Kind == ChannelKind.Base);

        Assert.Multiple(() =>
        {
            Assert.That(baseChannel.Scale, Is.EqualTo(FibScale.Log));
            Assert.That((double)baseChannel.Baseline.Slope, Is.EqualTo(0.013118213).Within(1e-6));
            Assert.That((double)baseChannel.Baseline.Intercept, Is.EqualTo(4.605170186).Within(1e-6));
            Assert.That((double)baseChannel.Parallel.Intercept, Is.EqualTo(4.943991683).Within(1e-6));
        });
    }

    [Test]
    public void Project_AccelerationChannel_Linear_ProjectsWave5Band()
    {
        // Baseline 2→4: (210-130)/(40-20)=4, intercept 130-4·20=50. Parallel through 3: 250-4·30=130.
        // Project at x* = 40 + (40-20) = 60 → baseline 4·60+50=290, parallel 4·60+130=370.
        var channels = ChannelProjector.Project(Impulse(), FibScale.Linear);
        var accel = channels.Single(c => c.Kind == ChannelKind.Acceleration);

        Assert.Multiple(() =>
        {
            Assert.That(accel.Baseline.Slope, Is.EqualTo(4m).Within(1e-6m));
            Assert.That(accel.Baseline.Intercept, Is.EqualTo(50m).Within(1e-6m));
            Assert.That(accel.Parallel.Intercept, Is.EqualTo(130m).Within(1e-6m));
            Assert.That(accel.TargetLow, Is.EqualTo(290m).Within(1e-6m));
            Assert.That(accel.TargetHigh, Is.EqualTo(370m).Within(1e-6m));
        });
    }

    [Test]
    public void Project_FewerThanThreePivots_ReturnsEmpty()
        => Assert.That(ChannelProjector.Project([new(T0, 100m, "0"), new(T0.AddDays(1), 110m, "1")], FibScale.Linear), Is.Empty);

    [Test]
    public void Project_OnlyThreePivots_HasBaseChannelButNoAcceleration()
    {
        var channels = ChannelProjector.Project([.. Impulse().Take(3)], FibScale.Linear);

        Assert.Multiple(() =>
        {
            Assert.That(channels, Has.Count.EqualTo(1));
            Assert.That(channels[0].Kind, Is.EqualTo(ChannelKind.Base));
        });
    }

    [Test]
    public void Project_LogWithNonPositivePrice_ReturnsEmpty()
    {
        List<WaveAnnotation> withZero = [new(T0, 0m, "0"), .. Impulse().Skip(1)];

        Assert.That(ChannelProjector.Project(withZero, FibScale.Log), Is.Empty);
    }
}
