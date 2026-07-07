namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A related instrument's measured relationship to the analyzed symbol over the count's window: its
/// correlation coefficient (computed by <see cref="Application.CorrelationCalculator"/>) and its own
/// price move over the same window. Input to <see cref="Application.IntermarketDivergenceDetector"/>.
/// </summary>
/// <param name="Symbol">The related instrument, e.g. "DXY" or a sector peer.</param>
/// <param name="Correlation">Pearson correlation of daily returns in [-1, 1] over the count's window.</param>
/// <param name="PercentChange">The related instrument's own percent move over the same window.</param>
public sealed record IntermarketReading(string Symbol, double Correlation, decimal PercentChange);
