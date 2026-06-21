namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Deprecated: use <see cref="ILlmWaveAnalyzer"/> instead.
/// Kept for binary compatibility only — will be removed in a future version.
/// </summary>
[Obsolete("Use ILlmWaveAnalyzer. IGeminiWaveAnalyzer will be removed in a future release.")]
public interface IGeminiWaveAnalyzer : ILlmWaveAnalyzer { }
