using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// <see cref="CatalystWindowFlagger"/>: flags calendar catalysts within a day-window of a count's
/// projected turn dates (#188, AC2).
/// </summary>
[TestFixture]
public sealed class CatalystWindowFlaggerTests
{
    private static readonly DateTime TurnDate = new(2026, 3, 15);

    [Test]
    public void Flag_CatalystWithinWindow_Fires()
    {
        var catalyst = new CatalystEvent(new DateTime(2026, 3, 17), "FOMC Rate Decision", "TestCalendar");

        var flags = CatalystWindowFlagger.Flag([catalyst], [TurnDate], windowDays: 3);

        Assert.Multiple(() =>
        {
            Assert.That(flags, Has.Count.EqualTo(1));
            Assert.That(flags[0].Event, Is.EqualTo(catalyst));
            Assert.That(flags[0].TurnDate, Is.EqualTo(TurnDate));
            Assert.That(flags[0].DaysFromTurn, Is.EqualTo(2));
        });
    }

    [Test]
    public void Flag_CatalystOutsideWindow_DoesNotFire()
    {
        var catalyst = new CatalystEvent(new DateTime(2026, 4, 1), "Q1 Earnings", "TestCalendar");

        var flags = CatalystWindowFlagger.Flag([catalyst], [TurnDate], windowDays: 3);

        Assert.That(flags, Is.Empty);
    }

    [Test]
    public void Flag_ExactlyAtWindowBoundary_Fires()
    {
        var catalyst = new CatalystEvent(TurnDate.AddDays(3), "CPI Release", "TestCalendar");

        var flags = CatalystWindowFlagger.Flag([catalyst], [TurnDate], windowDays: 3);

        Assert.That(flags, Has.Count.EqualTo(1));
    }

    [Test]
    public void Flag_MultipleTurnDates_PicksTheNearestOne()
    {
        DateTime[] turns = [new(2026, 3, 10), new(2026, 3, 20)];
        var catalyst = new CatalystEvent(new DateTime(2026, 3, 18), "Earnings", "TestCalendar");

        var flags = CatalystWindowFlagger.Flag([catalyst], turns, windowDays: 10);

        Assert.That(flags[0].TurnDate, Is.EqualTo(new DateTime(2026, 3, 20)));
    }

    [Test]
    public void Flag_NoTurnDates_ReturnsEmpty()
    {
        var catalyst = new CatalystEvent(TurnDate, "FOMC", "TestCalendar");
        Assert.That(CatalystWindowFlagger.Flag([catalyst], [], windowDays: 5), Is.Empty);
    }

    [Test]
    public void Flag_NegativeWindow_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CatalystWindowFlagger.Flag([], [TurnDate], windowDays: -1));
    }

    [Test]
    public void Flag_NullArguments_Throw()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() => CatalystWindowFlagger.Flag(null!, [TurnDate], 3));
            Assert.Throws<ArgumentNullException>(() => CatalystWindowFlagger.Flag([], null!, 3));
        });
    }
}
