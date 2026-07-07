using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>Request body for <c>POST /api/wave-analysis</c>.</summary>
/// <param name="Symbol">Instrument the annotations belong to.</param>
/// <param name="Annotations">The analyst's pivots, as placed on the chart.</param>
/// <param name="Interval">
/// Candle timeframe the pivots were placed on ('1h', '4h', '1d', '1w'; default daily). The
/// deterministic re-verify must snap against the same series the chart displayed — a weekly
/// bar's extreme prints on a different calendar day than the bar's own date, so snapping
/// weekly-placed pivots against daily candles would wrongly reject them.
/// </param>
public sealed record WaveValidationRequest(
    string Symbol,
    IReadOnlyList<WaveAnnotation> Annotations,
    string? Interval = null);
