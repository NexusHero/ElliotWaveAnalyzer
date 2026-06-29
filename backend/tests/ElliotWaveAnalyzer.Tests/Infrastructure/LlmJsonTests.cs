using ElliotWaveAnalyzer.Api.Infrastructure.Llm;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Tests for <see cref="LlmJson.ExtractObject"/>, which rescues the JSON object from the
/// markdown fences / stray prose models sometimes emit despite "JSON only" instructions.
/// </summary>
[TestFixture]
public sealed class LlmJsonTests
{
    [Test]
    public void PlainObject_ReturnedAsIs()
    {
        Assert.That(LlmJson.ExtractObject("""{"a":1}"""), Is.EqualTo("""{"a":1}"""));
    }

    [Test]
    public void FencedJson_FenceStripped()
    {
        const string input = "```json\n{\"a\":1}\n```";
        Assert.That(LlmJson.ExtractObject(input), Is.EqualTo("{\"a\":1}"));
    }

    [Test]
    public void LeadingBacktickAndProse_ObjectExtracted()
    {
        const string input = "`Here is the result: {\"a\":1} hope that helps`";
        Assert.That(LlmJson.ExtractObject(input), Is.EqualTo("""{"a":1}"""));
    }

    [Test]
    public void NestedBraces_OutermostObjectKept()
    {
        const string input = "noise {\"a\":{\"b\":2}} trailing";
        Assert.That(LlmJson.ExtractObject(input), Is.EqualTo("""{"a":{"b":2}}"""));
    }

    [Test]
    public void NoBraces_FallsBackToTrimmedInput()
    {
        Assert.That(LlmJson.ExtractObject("  not json  "), Is.EqualTo("not json"));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Blank_ReturnsEmpty(string input)
    {
        Assert.That(LlmJson.ExtractObject(input), Is.Empty);
    }
}
