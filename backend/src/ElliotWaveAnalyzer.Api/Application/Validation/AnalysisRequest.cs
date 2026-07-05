namespace ElliotWaveAnalyzer.Api.Application.Validation;

/// <summary>
/// Symbol and interval input for a simplified chart analysis request.
/// Symbol is validated against an allow-list so arbitrary strings
/// are never forwarded to upstream market-data APIs or LLM prompts.
/// </summary>
public record AnalysisRequest(string Symbol, string Interval, int Limit);
