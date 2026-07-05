namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>Response of <c>POST /api/analyses</c>: the id of the newly saved analysis.</summary>
public sealed record SavedAnalysisResponse(Guid Id);
