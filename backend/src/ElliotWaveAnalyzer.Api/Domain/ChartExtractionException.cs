namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Thrown when the vision model's output cannot be parsed into a valid <see cref="ChartExtraction"/>
/// after the allowed retries — surfaced to the client as a 422 (unprocessable) rather than a 500.
/// </summary>
public sealed class ChartExtractionException(string message) : Exception(message);
