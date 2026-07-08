namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>Request body to set the caller's narrative-language preference (#228).</summary>
public sealed record SetNarrativeLanguageRequest(NarrativeLanguage Language);
