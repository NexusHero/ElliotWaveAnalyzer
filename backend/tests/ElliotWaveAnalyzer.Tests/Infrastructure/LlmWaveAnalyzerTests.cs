using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure.Llm;
using ElliotWaveAnalyzer.Tests.Acceptance;
using ElliotWaveAnalyzer.Tests.TestData;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="LlmWaveAnalyzer"/>.
///
/// The LLM is the mockable <see cref="IChatClient"/> abstraction, so we can test the
/// parts that are genuinely ours — JSON parsing, fence stripping, empty/invalid
/// response handling, and token-usage mapping — without any network call.
/// </summary>
[TestFixture]
public sealed class LlmWaveAnalyzerTests
{
    private IChatClient _chat = null!;
    private LlmWaveAnalyzer _sut = null!;

    private static readonly IReadOnlyList<MarketCandle> Candles = MarketDataFixtures.CreateCandles(30);

    private static readonly IReadOnlyList<WaveAnnotation> Annotations =
    [
        new(new DateTime(2024, 1, 5, 0, 0, 0, DateTimeKind.Utc), 38_000m, "1"),
        new(new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc), 35_000m, "2"),
    ];

    private static readonly WaveRuleReport Report = new(BullishAssumed: false, Rules: [], Ratios: []);

    [SetUp]
    public void SetUp()
    {
        _chat = Substitute.For<IChatClient>();
        _sut = new LlmWaveAnalyzer(
            _chat,
            Options.Create(new LlmProviderOptions { Active = "Gemini" }),
            new FakeNarrativeLanguageProvider(),
            NullLogger<LlmWaveAnalyzer>.Instance);
    }

    private void GivenResponse(string text, UsageDetails? usage = null)
    {
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
        {
            Usage = usage,
        };

        _chat.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));
    }

    // ─── Happy path ─────────────────────────────────────────────────────────────

    [Test]
    public async Task ValidateAsync_ParsesValidJson()
    {
        GivenResponse("""
            { "isValid": true, "violations": [], "warnings": ["check wave 2"],
              "analysis": "Clean impulse.", "confidence": "high" }
            """);

        var validation = await _sut.ValidateAsync("BTC", Candles, Annotations, Report);

        Assert.That(validation.Result.IsValid, Is.True);
        Assert.That(validation.Result.Warnings, Does.Contain("check wave 2"));
        Assert.That(validation.Result.Analysis, Is.EqualTo("Clean impulse."));
        Assert.That(validation.Result.Confidence, Is.EqualTo("high"));
    }

    [Test]
    public async Task ValidateAsync_StripsMarkdownFences()
    {
        GivenResponse("""
            ```json
            { "isValid": false, "violations": ["wave 3 shortest"], "warnings": [],
              "analysis": "Invalid.", "confidence": "medium" }
            ```
            """);

        var validation = await _sut.ValidateAsync("BTC", Candles, Annotations, Report);

        Assert.That(validation.Result.IsValid, Is.False);
        Assert.That(validation.Result.Violations, Does.Contain("wave 3 shortest"));
    }

    [Test]
    public async Task ValidateAsync_MapsTokenUsage()
    {
        GivenResponse(
            """{ "isValid": true, "violations": [], "warnings": [], "analysis": "ok", "confidence": "low" }""",
            new UsageDetails { InputTokenCount = 120, OutputTokenCount = 30, TotalTokenCount = 150 });

        var validation = await _sut.ValidateAsync("BTC", Candles, Annotations, Report);

        Assert.That(validation.Usage.Provider, Is.EqualTo("Gemini"));
        Assert.That(validation.Usage.PromptTokens, Is.EqualTo(120));
        Assert.That(validation.Usage.CompletionTokens, Is.EqualTo(30));
        Assert.That(validation.Usage.TotalTokens, Is.EqualTo(150));
    }

    [Test]
    public async Task ValidateAsync_NoUsageReported_DefaultsToZero()
    {
        GivenResponse(
            """{ "isValid": true, "violations": [], "warnings": [], "analysis": "ok", "confidence": "low" }""");

        var validation = await _sut.ValidateAsync("BTC", Candles, Annotations, Report);

        Assert.That(validation.Usage.TotalTokens, Is.Zero);
    }

    // ─── Failure handling ────────────────────────────────────────────────────────

    [Test]
    public void ValidateAsync_EmptyResponse_Throws()
    {
        GivenResponse("   ");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ValidateAsync("BTC", Candles, Annotations, Report));
    }

    [Test]
    public void ValidateAsync_InvalidJson_Throws()
    {
        GivenResponse("not json at all");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ValidateAsync("BTC", Candles, Annotations, Report));
    }

    // ─── Narrative language (#228) ─────────────────────────────────────────────

    [Test]
    public async Task ValidateAsync_GermanPreference_AppendsTheLanguageDirectiveToTheSystemPrompt()
    {
        GivenResponse(
            """{ "isValid": true, "violations": [], "warnings": [], "analysis": "x", "confidence": "high" }""");
        var sut = new LlmWaveAnalyzer(
            _chat,
            Options.Create(new LlmProviderOptions { Active = "Gemini" }),
            new FakeNarrativeLanguageProvider { Language = NarrativeLanguage.German },
            NullLogger<LlmWaveAnalyzer>.Instance);

        await sut.ValidateAsync("BTC", Candles, Annotations, Report);

        await _chat.Received().GetResponseAsync(
            Arg.Is<IEnumerable<ChatMessage>>(msgs =>
                msgs.Any(m => m.Role == ChatRole.System && m.Text!.Contains("German"))),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    // ─── Provider name ───────────────────────────────────────────────────────────

    [Test]
    public void ProviderName_ReflectsActiveConfig()
    {
        var sut = new LlmWaveAnalyzer(
            _chat,
            Options.Create(new LlmProviderOptions { Active = "Claude" }),
            new FakeNarrativeLanguageProvider(),
            NullLogger<LlmWaveAnalyzer>.Instance);

        Assert.That(sut.ProviderName, Is.EqualTo("Claude"));
    }
}
