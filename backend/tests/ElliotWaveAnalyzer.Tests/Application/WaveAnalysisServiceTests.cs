using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using ElliotWaveAnalyzer.Tests.TestData;
using NSubstitute;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Unit tests for <see cref="WaveAnalysisService"/>.
///
/// Tests focus on orchestration:
/// - Input validation (empty annotations, invalid labels, wrong order)
/// - Correct candle fetching for the annotated period
/// - Correct delegation to IGeminiWaveAnalyzer
/// - Result pass-through
///
/// IGeminiWaveAnalyzer is mocked — network calls to Gemini are never made here.
/// </summary>
[TestFixture]
public sealed class WaveAnalysisServiceTests
{
    private IMarketDataProvider _provider = null!;
    private IGeminiWaveAnalyzer _gemini = null!;
    private IWaveAnalysisService _sut = null!;

    private static readonly WaveValidationResult ValidResult = new(
        IsValid: true,
        Violations: [],
        Warnings: [],
        Analysis: "Clean 5-wave impulse.",
        Confidence: "high");

    private static readonly IReadOnlyList<WaveAnnotation> ValidAnnotations =
    [
        new(new DateTime(2024, 1,  5, 0, 0, 0, DateTimeKind.Utc), 38_000m, "1"),
        new(new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc), 35_000m, "2"),
        new(new DateTime(2024, 2,  1, 0, 0, 0, DateTimeKind.Utc), 52_000m, "3"),
    ];

    [SetUp]
    public void SetUp()
    {
        _provider = Substitute.For<IMarketDataProvider>();
        _gemini = Substitute.For<IGeminiWaveAnalyzer>();

        _provider.Supports(Arg.Any<string>()).Returns(true);
        _provider.GetCandlesAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(MarketDataFixtures.CreateCandles(90)));

        _gemini.ValidateAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<MarketCandle>>(),
                Arg.Any<IReadOnlyList<WaveAnnotation>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ValidResult));

        _sut = new WaveAnalysisService(
            providers: [_provider],
            geminiAnalyzer: _gemini);
    }

    // ─── Input validation ─────────────────────────────────────────────────────

    [Test]
    public void ValidateAsync_EmptyAnnotations_ThrowsArgumentException()
    {
        var ex = Assert.ThrowsAsync<ArgumentException>(
            () => _sut.ValidateAsync("BTC", []));

        Assert.That(ex!.Message, Does.Contain("annotation").IgnoreCase);
    }

    [Test]
    public void ValidateAsync_SingleAnnotation_ThrowsArgumentException()
    {
        var single = new List<WaveAnnotation>
            { new(DateTime.UtcNow, 40_000m, "1") };

        Assert.ThrowsAsync<ArgumentException>(
            () => _sut.ValidateAsync("BTC", single));
    }

    [Test]
    public void ValidateAsync_InvalidLabel_ThrowsArgumentException()
    {
        var badAnnotations = new List<WaveAnnotation>
        {
            new(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), 40_000m, "1"),
            new(new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), 38_000m, "INVALID"),
        };

        var ex = Assert.ThrowsAsync<ArgumentException>(
            () => _sut.ValidateAsync("BTC", badAnnotations));

        Assert.That(ex!.Message, Does.Contain("INVALID"));
    }

    [Test]
    public void ValidateAsync_AnnotationsNotChronological_ThrowsArgumentException()
    {
        var outOfOrder = new List<WaveAnnotation>
        {
            new(new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc), 42_000m, "1"),
            new(new DateTime(2024, 1,  5, 0, 0, 0, DateTimeKind.Utc), 38_000m, "2"), // date before previous
        };

        Assert.ThrowsAsync<ArgumentException>(
            () => _sut.ValidateAsync("BTC", outOfOrder));
    }

    // ─── Candle fetching ──────────────────────────────────────────────────────

    [Test]
    public async Task ValidateAsync_FetchesCandlesForSymbol()
    {
        await _sut.ValidateAsync("BTC", ValidAnnotations);

        await _provider.Received(1).GetCandlesAsync(
            "BTC", Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ValidateAsync_FetchesEnoughDaysToCoversAnnotations()
    {
        // Annotations span 27 days (Jan 5 → Feb 1). Service should fetch at least that.
        await _sut.ValidateAsync("BTC", ValidAnnotations);

        await _provider.Received(1).GetCandlesAsync(
            "BTC",
            Arg.Is<int>(days => days >= 27),
            Arg.Any<CancellationToken>());
    }

    // ─── Gemini delegation ────────────────────────────────────────────────────

    [Test]
    public async Task ValidateAsync_DelegatesToGemini_WithCorrectSymbol()
    {
        await _sut.ValidateAsync("ETH", ValidAnnotations);

        await _gemini.Received(1).ValidateAsync(
            "ETH",
            Arg.Any<IReadOnlyList<MarketCandle>>(),
            Arg.Any<IReadOnlyList<WaveAnnotation>>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ValidateAsync_DelegatesToGemini_WithCorrectAnnotations()
    {
        await _sut.ValidateAsync("BTC", ValidAnnotations);

        await _gemini.Received(1).ValidateAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<MarketCandle>>(),
            Arg.Is<IReadOnlyList<WaveAnnotation>>(a =>
                a.Count == ValidAnnotations.Count &&
                a[0].Label == "1" && a[1].Label == "2"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ValidateAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();

        await _sut.ValidateAsync("BTC", ValidAnnotations, cts.Token);

        await _gemini.Received(1).ValidateAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<MarketCandle>>(),
            Arg.Any<IReadOnlyList<WaveAnnotation>>(),
            cts.Token);
    }

    // ─── Result pass-through ──────────────────────────────────────────────────

    [Test]
    public async Task ValidateAsync_ReturnsGeminiResult()
    {
        var result = await _sut.ValidateAsync("BTC", ValidAnnotations);

        Assert.That(result, Is.SameAs(ValidResult));
    }

    [Test]
    public async Task ValidateAsync_WithViolations_ReturnsThemUnchanged()
    {
        var withViolation = new WaveValidationResult(
            IsValid: false,
            Violations: ["Wave 3 is the shortest impulse wave — violates core rule"],
            Warnings: [],
            Analysis: "Invalid count.",
            Confidence: "high");

        _gemini.ValidateAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<MarketCandle>>(),
                Arg.Any<IReadOnlyList<WaveAnnotation>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(withViolation));

        var result = await _sut.ValidateAsync("BTC", ValidAnnotations);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Violations, Has.Count.EqualTo(1));
        Assert.That(result.Violations[0], Does.Contain("Wave 3"));
    }
}
