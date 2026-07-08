namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The caller's narrative-language preference (#228). <see cref="Language"/> is null when the user
/// has never explicitly chosen one — the frontend uses that to suggest (and persist) a default from
/// the browser's locale (AC4); narrators otherwise treat an unset preference as English.
/// </summary>
public sealed record NarrativeLanguageResponse(NarrativeLanguage? Language);
