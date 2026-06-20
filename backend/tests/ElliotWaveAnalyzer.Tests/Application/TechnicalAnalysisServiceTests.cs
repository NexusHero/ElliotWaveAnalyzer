using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using ElliotWaveAnalyzer.Tests.TestData;
using NSubstitute;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Unit tests for <see cref="TechnicalAnalysisService"/>.
///
/// All external dependencies (providers, calculator) are substituted.
/// We test orchestration logic only — not indicator math (that's in
/// <see cref="ElliotWaveAnalyzer.Tests.Infrastructure.SkenderIndicatorCalculatorTests"/>).
/// </summary>
[TestFixture]
public sealed class TechnicalAnalysisServiceTests
{
    private IMarketDataProvider _btcProvider = null!;
    private IMarketDataProvider _ethProvider = null!;
    private IIndicatorCalculator _calculator = null!;
    private ITechnicalAnalysisService _sut = null!;

    private static readonly IReadOnlyList<MacdResult> EmptyMacd = Array.Empty<MacdResult>();
    private static readonly IReadOnlyList<RsiResult> EmptyRsi = Array.Empty<RsiResult>();

    [SetUp]
    public void SetUp()
    {
        _btcProvider = Substitute.For<IMarketDataProvider>();
        _ethProvider = Substitute.For<IMarketDataProvider>();
        _calculator = Substitute.For<IIndicatorCalculator>();

        // BTC provider handles BTC only, ETH provider handles ETH only.
        _btcProvider.Supports("BTC").Returns(true);
        _btcProvider.Supports(Arg.Is<string>(s => s != "BTC")).Returns(false);
        _ethProvider.Supports("ETH").Returns(true);
        _ethProvider.Supports(Arg.Is<string>(s => s != "ETH")).Returns(false);

        _sut = new TechnicalAnalysisService(
            providers: [_btcProvider, _ethProvider],
            calculator: _calculator);
    }

    // ─── Provider selection ───────────────────────────────────────────────────

    [Test]
    public async Task GetAnalysisAsync_BtcSymbol_DelegatesOnlyToBtcProvider()
    {
        var candles = MarketDataFixtures.CreateCandles(30);
        ArrangeCalculatorReturnsEmpty(candles);
        _btcProvider.GetCandlesAsync("BTC", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(candles));

        await _sut.GetAnalysisAsync("BTC");

        await _btcProvider.Received(1).GetCandlesAsync("BTC", Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _ethProvider.DidNotReceive().GetCandlesAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetAnalysisAsync_EthSymbol_DelegatesOnlyToEthProvider()
    {
        var candles = MarketDataFixtures.CreateCandles(30);
        ArrangeCalculatorReturnsEmpty(candles);
        _ethProvider.GetCandlesAsync("ETH", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(candles));

        await _sut.GetAnalysisAsync("ETH");

        await _ethProvider.Received(1).GetCandlesAsync("ETH", Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _btcProvider.DidNotReceive().GetCandlesAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void GetAnalysisAsync_UnsupportedSymbol_ThrowsArgumentException()
    {
        var ex = Assert.ThrowsAsync<ArgumentException>(
            () => _sut.GetAnalysisAsync("DOGE"));

        Assert.That(ex!.Message, Does.Contain("DOGE"),
            "Exception message should identify the unknown symbol");
    }

    // ─── Correct days forwarded ───────────────────────────────────────────────

    [Test]
    public async Task GetAnalysisAsync_ForwardsDaysToProvider()
    {
        const int requestedDays = 180;
        var candles = MarketDataFixtures.CreateCandles(10);
        ArrangeCalculatorReturnsEmpty(candles);
        _btcProvider.GetCandlesAsync("BTC", requestedDays, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(candles));

        await _sut.GetAnalysisAsync("BTC", days: requestedDays);

        await _btcProvider.Received(1).GetCandlesAsync("BTC", requestedDays, Arg.Any<CancellationToken>());
    }

    // ─── Result shape ─────────────────────────────────────────────────────────

    [Test]
    public async Task GetAnalysisAsync_ResultContainsCorrectSymbol()
    {
        var candles = ArrangeBtcWithCandles(30);

        var result = await _sut.GetAnalysisAsync("BTC");

        Assert.That(result.Symbol, Is.EqualTo("BTC"));
    }

    [Test]
    public async Task GetAnalysisAsync_ResultCandles_AreExactlyFromProvider()
    {
        var candles = ArrangeBtcWithCandles(30);

        var result = await _sut.GetAnalysisAsync("BTC");

        Assert.That(result.Candles, Is.SameAs(candles),
            "Service must not copy or transform candles — pass the provider's list through");
    }

    [Test]
    public async Task GetAnalysisAsync_InvokesCalculatorForBothRsiAndMacd()
    {
        var candles = ArrangeBtcWithCandles(30);

        await _sut.GetAnalysisAsync("BTC");

        _calculator.Received(1).CalculateRsi(candles, Arg.Any<int>());
        _calculator.Received(1).CalculateMacd(candles, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [Test]
    public async Task GetAnalysisAsync_ResultMacdAndRsi_ComeFromCalculator()
    {
        var candles = ArrangeBtcWithCandles(30);
        var expectedMacd = new[] { new MacdResult(DateTime.UtcNow, 1m, 0.9m, 0.1m) };
        var expectedRsi = new[] { new RsiResult(DateTime.UtcNow, 55m) };

        _calculator.CalculateMacd(candles, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(expectedMacd);
        _calculator.CalculateRsi(candles, Arg.Any<int>())
            .Returns(expectedRsi);

        var result = await _sut.GetAnalysisAsync("BTC");

        Assert.That(result.Macd, Is.SameAs(expectedMacd));
        Assert.That(result.Rsi, Is.SameAs(expectedRsi));
    }

    // ─── CancellationToken propagation ───────────────────────────────────────

    [Test]
    public async Task GetAnalysisAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var candles = MarketDataFixtures.CreateCandles(10);
        ArrangeCalculatorReturnsEmpty(candles);
        _btcProvider.GetCandlesAsync("BTC", Arg.Any<int>(), cts.Token)
            .Returns(Task.FromResult(candles));

        await _sut.GetAnalysisAsync("BTC", cancellationToken: cts.Token);

        await _btcProvider.Received(1).GetCandlesAsync("BTC", Arg.Any<int>(), cts.Token);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private IReadOnlyList<MarketCandle> ArrangeBtcWithCandles(int count)
    {
        var candles = MarketDataFixtures.CreateCandles(count);
        _btcProvider.GetCandlesAsync("BTC", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(candles));
        ArrangeCalculatorReturnsEmpty(candles);
        return candles;
    }

    private void ArrangeCalculatorReturnsEmpty(IReadOnlyList<MarketCandle> candles)
    {
        _calculator.CalculateRsi(candles, Arg.Any<int>()).Returns(EmptyRsi);
        _calculator.CalculateMacd(candles, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>()).Returns(EmptyMacd);
    }
}
