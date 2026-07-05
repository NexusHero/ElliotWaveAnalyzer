namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Thrown when a requested candle range exceeds what the data source can serve for the requested
/// interval (e.g. hourly bars are only available ~2 years back). Deliberately a dedicated type so
/// callers surface the limit to the user instead of silently truncating — honest degradation.
/// </summary>
public sealed class MarketDataRangeException(string message, int maxDays) : Exception(message)
{
    /// <summary>The maximum number of days the source supports for the requested interval.</summary>
    public int MaxDays { get; } = maxDays;
}
