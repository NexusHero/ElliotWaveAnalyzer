using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// <see cref="NarrativeLanguageDirective"/>: the single source of truth every narrator/prompt
/// caller appends the same wording from (#228).
/// </summary>
[TestFixture]
public sealed class NarrativeLanguageDirectiveTests
{
    [Test]
    public void For_English_ReturnsNull_NothingToAppend()
        => Assert.That(NarrativeLanguageDirective.For(NarrativeLanguage.English), Is.Null);

    [Test]
    public void For_German_ReturnsANonEmptyDirectiveMentioningGerman()
    {
        var directive = NarrativeLanguageDirective.For(NarrativeLanguage.German);

        Assert.That(directive, Is.Not.Null.And.Not.Empty);
        Assert.That(directive, Does.Contain("German"));
    }
}
