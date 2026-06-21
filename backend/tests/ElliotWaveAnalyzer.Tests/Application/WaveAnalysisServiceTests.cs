using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using ElliotWaveAnalyzer.Tests.TestData;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Unit tests for <see cref="WaveAnalysisService"/>.
/// All external dependencies are mocked — no real LLM or market data calls.
/// </summary>
[TestFixture]
public sealed class WaveAnalysisServiceTests
{
    private IMarketDataProvider _provider = null!;
    private ILlmWaveAnalyzer _llm = null!;
    private ITokenTracker _tokenTracker = null!;
    private IWaveAnalysisService _sut = null!;

    private static readonly TokenUsage DummyUsage = new("Gemini", 100, 50, 150);

    private static readonly WaveValidationResult ValidResult = new(
        IsValid: true,
        Violations: [],
        Warnings: [],
        Analysis: "Clean 5-wave impulse.",
        Confidence: "high");

    private static readonly LlmValidation ValidValidation = new(ValidResult, DummyUsage);

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
        _llm = Substitute.For<ILlmWaveAnalyzer>();
        _tokenTracker = Substitute.For<ITokenTracker>();

        _provider.Supports(Arg.Any<string>()).Returns(true);
        _provider.GetCandlesAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(MarketDataFixtures.CreateCandles(90)));

        _llm.ProviderName.Returns("Gemini");
        _llm.ValidateAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<MarketCandle>>(),
                Arg.Any<IReadOnlyList<WaveAnnotation>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ValidValidation));

        _tokenTracker.IsBudgetExceeded().Returns(false);

        _sut = BuildSut();
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
        var single = new List<WaveAnnotation> { new(DateTime.UtcNow, 40_000m, "1") };

        Assert.ThrowsAsync<ArgumentException>(() => _sut.ValidateAsync("BTC", single));
    }

    [Test]
    public void ValidateAsync_InvalidLabel_ThrowsArgumentException()
    {
        var bad = new List<WaveAnnotation>
        {
            new(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), 40_000m, "1"),
            new(new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), 38_000m, "INVALID"),
        };

        var ex = Assert.ThrowsAsync<ArgumentException>(() => _sut.ValidateAsync("BTC", bad));
        Assert.That(ex!.Message, Does.Contain("INVALID"));
    }

    [Test]
    public void ValidateAsync_AnnotationsNotChronological_ThrowsArgumentException()
    {
        var outOfOrder = new List<WaveAnnotation>
        {
            new(new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc), 42_000m, "1"),
            new(new DateTime(2024, 1,  5, 0, 0, 0, DateTimeKind.Utc), 38_000m, "2"),
        };

        Assert.ThrowsAsync<ArgumentException>(() => _sut.ValidateAsync("BTC", outOfOrder));
    }

    // ─── Budget enforcement ───────────────────────────────────────────────────

    [Test]
    public void ValidateAsync_BudgetExceeded_ThrowsInvalidOperationException()
    {
        _tokenTracker.IsBudgetExceeded().Returns(true);
        _tokenTracker.GetReport().Returns(new TokenUsageReport(
            SessionTotalTokens: 100_000,
            SessionCallCount: 10,
            Budget: 100_000,
            RemainingBudget: 0,
            IsBudgetExceeded: true,
            TokensByProvider: new Dictionary<string, int>()));

        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ValidateAsync("BTC", ValidAnnotations));

        Assert.That(ex!.Message, Does.Contain("budget").IgnoreCase);
        // LLM must NOT be called when budget is exceeded
        _llm.DidNotReceive().ValidateAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<MarketCandle>>(),
            Arg.Any<IReadOnlyList<WaveAnnotation>>(),
            Arg.Any<CancellationToken>());
    }

    // ─── Provider delegation ──────────────────────────────────────────────────

    [Test]
    public async Task ValidateAsync_DelegatesToLlmProvider()
    {
        await _sut.ValidateAsync("BTC", ValidAnnotations);

        await _llm.Received(1).ValidateAsync(
            "BTC",
            Arg.Any<IReadOnlyList<MarketCandle>>(),
            Arg.Any<IReadOnlyList<WaveAnnotation>>(),
            Arg.Any<CancellationToken>());
    }

    // ─── Token tracking ───────────────────────────────────────────────────────

    [Test]
    public async Task ValidateAsync_RecordsTokenUsageAfterSuccessfulCall()
    {
        await _sut.ValidateAsync("BTC", ValidAnnotations);

        _tokenTracker.Received(1).Record(DummyUsage);
    }

    // ─── Result pass-through ──────────────────────────────────────────────────

    [Test]
    public async Task ValidateAsync_ReturnsLlmValidation()
    {
        var result = await _sut.ValidateAsync("BTC", ValidAnnotations);

        Assert.That(result, Is.SameAs(ValidValidation));
    }

    [Test]
    public async Task ValidateAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();

        await _sut.ValidateAsync("BTC", ValidAnnotations, cts.Token);

        await _llm.Received(1).ValidateAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<MarketCandle>>(),
            Arg.Any<IReadOnlyList<WaveAnnotation>>(),
            cts.Token);
    }

    // ─── Candle fetching ──────────────────────────────────────────────────────

    [Test]
    public async Task ValidateAsync_FetchesEnoughDaysToCoverAnnotations()
    {
        // Annotations span 27 days — service should fetch at least that
        await _sut.ValidateAsync("BTC", ValidAnnotations);

        await _provider.Received(1).GetCandlesAsync(
            "BTC",
            Arg.Is<int>(days => days >= 27),
            Arg.Any<CancellationToken>());
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private WaveAnalysisService BuildSut() =>
        new(
            marketDataProviders: [_provider],
            llm: _llm,
            tokenTracker: _tokenTracker,
            logger: NullLogger<WaveAnalysisService>.Instance);
}
