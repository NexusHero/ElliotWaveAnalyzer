using System.Security.Claims;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure.Llm;
using ElliotWaveAnalyzer.Api.Interfaces;
using ElliotWaveAnalyzer.Tests.Acceptance;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// <see cref="HttpContextNarrativeLanguageProvider"/>: resolves the calling user from the session
/// principal and delegates to <see cref="INarrativeLanguageSettingsService"/> (#228); an anonymous
/// caller or a user with no stored preference resolves to English.
/// </summary>
[TestFixture]
public sealed class HttpContextNarrativeLanguageProviderTests
{
    private static IHttpContextAccessor AccessorFor(Guid? userId)
    {
        var httpContext = new DefaultHttpContext();
        if (userId is { } id)
        {
            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())]));
        }

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        return accessor;
    }

    [Test]
    public async Task GetCurrentAsync_NoHttpContextUser_ReturnsEnglish()
    {
        var provider = new HttpContextNarrativeLanguageProvider(
            AccessorFor(null), new FakeNarrativeLanguageSettingsService());

        Assert.That(await provider.GetCurrentAsync(), Is.EqualTo(NarrativeLanguage.English));
    }

    [Test]
    public async Task GetCurrentAsync_UserWithNoStoredPreference_ReturnsEnglish()
    {
        var provider = new HttpContextNarrativeLanguageProvider(
            AccessorFor(Guid.NewGuid()), new FakeNarrativeLanguageSettingsService());

        Assert.That(await provider.GetCurrentAsync(), Is.EqualTo(NarrativeLanguage.English));
    }

    [Test]
    public async Task GetCurrentAsync_UserWithGermanPreference_ReturnsGerman()
    {
        var userId = Guid.NewGuid();
        var settings = new FakeNarrativeLanguageSettingsService();
        await settings.SetAsync(userId, NarrativeLanguage.German);

        var provider = new HttpContextNarrativeLanguageProvider(AccessorFor(userId), settings);

        Assert.That(await provider.GetCurrentAsync(), Is.EqualTo(NarrativeLanguage.German));
    }
}
