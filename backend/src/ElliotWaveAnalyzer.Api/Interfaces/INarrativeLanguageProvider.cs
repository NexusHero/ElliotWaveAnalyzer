using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Resolves the narrative language to write in for the <b>currently authenticated caller</b>
/// (#228) — narrators and prompt callers that don't already receive a <c>userId</c> parameter
/// depend on this instead of threading one through their whole call chain. Never throws: an
/// anonymous caller (e.g. a background job) or a user who never set a preference resolves to
/// <see cref="NarrativeLanguage.English"/>.
/// </summary>
public interface INarrativeLanguageProvider
{
    /// <summary>The current caller's narrative language, defaulting to English.</summary>
    Task<NarrativeLanguage> GetCurrentAsync(CancellationToken cancellationToken = default);
}
