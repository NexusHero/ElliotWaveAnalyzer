namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The price scale Fibonacci relationships are measured on. Linear is correct for small ranges;
/// log is correct for instruments spanning multiples of their base price (a stock from $3 to $145),
/// where a linear 61.8% retracement is visibly wrong. Auto-selected by range and always reported.
/// </summary>
public enum FibScale
{
    /// <summary>Arithmetic price differences.</summary>
    Linear,

    /// <summary>Ratios in log-price space (equal percentage moves are equal distances).</summary>
    Log,
}
