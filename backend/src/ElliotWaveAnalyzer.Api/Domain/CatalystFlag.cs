namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A catalyst that falls within the configured window of one of the count's projected turn dates —
/// produced only by <see cref="Application.CatalystWindowFlagger"/> (#188, AC2). A catalyst outside
/// every window never appears here.
/// </summary>
/// <param name="Event">The catalyst itself.</param>
/// <param name="TurnDate">The nearest projected turn date it falls within the window of.</param>
/// <param name="DaysFromTurn">Absolute calendar days between the catalyst and <see cref="TurnDate"/>.</param>
public sealed record CatalystFlag(CatalystEvent Event, DateTime TurnDate, int DaysFromTurn);
