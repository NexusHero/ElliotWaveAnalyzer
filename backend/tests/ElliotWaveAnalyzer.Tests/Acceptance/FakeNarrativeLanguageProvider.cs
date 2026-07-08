using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>Configurable <see cref="INarrativeLanguageProvider"/> fake. Defaults to English.</summary>
public sealed class FakeNarrativeLanguageProvider : INarrativeLanguageProvider
{
    public NarrativeLanguage Language { get; set; } = NarrativeLanguage.English;

    public Task<NarrativeLanguage> GetCurrentAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Language);
}
