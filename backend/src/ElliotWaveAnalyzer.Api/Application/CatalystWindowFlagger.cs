using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Flags calendar catalysts that fall within a configured day-window of one of a count's projected
/// turn dates (#188, AC2). Deterministic and pure — takes plain dates, never the count's
/// <see cref="WaveLevels"/> object itself, so it cannot interfere with the count's geometry (AC4).
/// </summary>
public static class CatalystWindowFlagger
{
    /// <summary>
    /// For each catalyst within <paramref name="windowDays"/> of at least one turn date, returns a
    /// flag against its <em>nearest</em> turn date. A catalyst outside every window is dropped, not
    /// included with a null/placeholder turn date.
    /// </summary>
    public static IReadOnlyList<CatalystFlag> Flag(
        IReadOnlyList<CatalystEvent> catalysts, IReadOnlyList<DateTime> turnDates, int windowDays)
    {
        ArgumentNullException.ThrowIfNull(catalysts);
        ArgumentNullException.ThrowIfNull(turnDates);
        if (windowDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowDays), windowDays, "Window must be non-negative.");
        }

        var flags = new List<CatalystFlag>();
        foreach (var catalyst in catalysts)
        {
            DateTime? nearestTurn = null;
            var nearestDistance = int.MaxValue;
            foreach (var turn in turnDates)
            {
                var distance = Math.Abs((catalyst.Date.Date - turn.Date).Days);
                if (distance <= windowDays && distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestTurn = turn;
                }
            }

            if (nearestTurn is { } turnDate)
            {
                flags.Add(new CatalystFlag(catalyst, turnDate, nearestDistance));
            }
        }

        return flags;
    }
}
