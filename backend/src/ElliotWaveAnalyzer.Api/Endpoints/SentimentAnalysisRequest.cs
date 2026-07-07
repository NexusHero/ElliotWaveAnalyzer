using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>Request body for <c>POST /api/wave-analysis/sentiment</c>.</summary>
/// <param name="Symbol">Instrument the pivots belong to.</param>
/// <param name="Annotations">
/// The count's pivots (the analyst's own or the auto-ranked count's) — mood-vs-position divergence is
/// detected against these dates and prices, exactly like <c>WaveValidationRequest.Annotations</c>.
/// </param>
public sealed record SentimentAnalysisRequest(
    string Symbol,
    IReadOnlyList<WaveAnnotation> Annotations);
