namespace ElliotWaveAnalyzer.Api.Infrastructure.Gemini;

/// <summary>
/// Strongly-typed configuration for the Gemini API.
/// Bound from appsettings.json → "Gemini" section via IOptions&lt;GeminiOptions&gt;.
///
/// WHY the model name is configurable:
/// Google deprecates Gemini model versions regularly. Changing the model
/// must never require a code change — only an appsettings update.
/// </summary>
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>Google AI Studio API key (set via appsettings or environment variable).</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// Gemini model identifier. Default: gemini-2.5-flash.
    /// Override in appsettings.json when Google releases a new stable model.
    /// </summary>
    public string Model { get; init; } = "gemini-2.5-flash";
}
