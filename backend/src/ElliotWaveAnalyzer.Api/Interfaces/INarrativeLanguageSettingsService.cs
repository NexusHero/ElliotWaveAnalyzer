using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Persists a user's narrative-language preference (#228). Every operation is scoped to the
/// calling user — no cross-user access.
/// </summary>
public interface INarrativeLanguageSettingsService
{
    /// <summary>The user's chosen language, or null when they have never set one.</summary>
    Task<NarrativeLanguage?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Sets the user's narrative-language preference.</summary>
    Task SetAsync(Guid userId, NarrativeLanguage language, CancellationToken cancellationToken = default);
}
