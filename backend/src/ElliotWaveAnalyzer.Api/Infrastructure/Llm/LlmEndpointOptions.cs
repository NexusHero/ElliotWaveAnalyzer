namespace ElliotWaveAnalyzer.Api.Infrastructure.Llm;

/// <summary>Per-provider API key and model name.</summary>
internal sealed class LlmEndpointOptions
{
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// Model identifier. Configurable so a new model can be adopted without
    /// a code change — just update appsettings and restart.
    /// </summary>
    public string Model { get; init; } = string.Empty;
}
