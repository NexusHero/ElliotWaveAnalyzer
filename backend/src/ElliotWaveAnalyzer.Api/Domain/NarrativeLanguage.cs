namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The language LLM-generated narrative prose (market reads, coach reflections, analog/portfolio
/// summaries, hypothesis reasons) is written in (#228). Deterministic output — rule names, level
/// labels, JSON keys — is unaffected; only free-text fields honour this.
/// </summary>
public enum NarrativeLanguage
{
    /// <summary>The base language every prompt is already written in — no extra instruction needed.</summary>
    English,

    German,
}
