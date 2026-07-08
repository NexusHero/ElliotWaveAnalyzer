using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>In-memory <see cref="INarrativeLanguageSettingsService"/> fake, keyed by user id.</summary>
public sealed class FakeNarrativeLanguageSettingsService : INarrativeLanguageSettingsService
{
    private readonly Dictionary<Guid, NarrativeLanguage> _byUser = [];

    public Task<NarrativeLanguage?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult(_byUser.TryGetValue(userId, out var language) ? language : (NarrativeLanguage?)null);

    public Task SetAsync(Guid userId, NarrativeLanguage language, CancellationToken cancellationToken = default)
    {
        _byUser[userId] = language;
        return Task.CompletedTask;
    }
}
