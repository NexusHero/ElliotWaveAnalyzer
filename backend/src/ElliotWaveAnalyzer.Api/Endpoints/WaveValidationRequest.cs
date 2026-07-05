using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>Request body for <c>POST /api/wave-analysis</c>.</summary>
public sealed record WaveValidationRequest(
    string Symbol,
    IReadOnlyList<WaveAnnotation> Annotations);
