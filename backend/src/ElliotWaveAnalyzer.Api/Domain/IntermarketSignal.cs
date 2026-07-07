namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// One related instrument's classified relationship to the count's thesis — produced only by
/// <see cref="Application.IntermarketDivergenceDetector"/> (#188, AC3). Carries the raw
/// <see cref="Correlation"/> and <see cref="PercentChange"/> it was classified from, so the narrative
/// (and its fact-guard) can cite the exact computed numbers rather than a bare label.
/// </summary>
/// <param name="Symbol">The related instrument.</param>
/// <param name="Correlation">The correlation the classification was based on.</param>
/// <param name="PercentChange">The instrument's own percent move over the count's window.</param>
/// <param name="Kind">Whether this reading supports or contradicts the count's thesis.</param>
public sealed record IntermarketSignal(
    string Symbol, double Correlation, decimal PercentChange, IntermarketSignalKind Kind);
