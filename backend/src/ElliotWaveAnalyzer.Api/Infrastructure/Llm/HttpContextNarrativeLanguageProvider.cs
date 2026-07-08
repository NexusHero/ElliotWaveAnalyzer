using System.Security.Claims;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Llm;

/// <summary>
/// Resolves the current request's narrative language from its session principal, delegating the
/// actual lookup to <see cref="INarrativeLanguageSettingsService"/> (#228). Mirrors
/// <see cref="UserAwareChatClient"/>'s own "resolve the caller from <see cref="IHttpContextAccessor"/>"
/// shape, so narrators depend on this rather than needing a <c>userId</c> parameter added to their
/// public contract.
/// </summary>
internal sealed class HttpContextNarrativeLanguageProvider(
    IHttpContextAccessor httpContextAccessor,
    INarrativeLanguageSettingsService settings) : INarrativeLanguageProvider
{
    /// <inheritdoc/>
    public async Task<NarrativeLanguage> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var id = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(id, out var userId))
        {
            return NarrativeLanguage.English;
        }

        return await settings.GetAsync(userId, cancellationToken) ?? NarrativeLanguage.English;
    }
}
