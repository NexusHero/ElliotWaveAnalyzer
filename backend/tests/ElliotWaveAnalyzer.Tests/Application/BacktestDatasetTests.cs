using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Tests.TestData;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// The dataset hash is the run's identity: stable for the same candles + config (so a re-run is
/// idempotent) and different when either the candles or the config change.
/// </summary>
[TestFixture]
public sealed class BacktestDatasetTests
{
    private static readonly IReadOnlyList<MarketCandle> Candles = MarketDataFixtures.CreateCandles(50);
    private static readonly BacktestConfig Config = new();

    [Test]
    public void Hash_SameCandlesAndConfig_IsStable()
        => Assert.That(BacktestDataset.Hash(Candles, Config), Is.EqualTo(BacktestDataset.Hash(Candles, Config)));

    [Test]
    public void Hash_DifferentConfig_Differs()
    {
        var other = Config with { Step = Config.Step + 1 };
        Assert.That(BacktestDataset.Hash(Candles, other), Is.Not.EqualTo(BacktestDataset.Hash(Candles, Config)));
    }

    [Test]
    public void Hash_DifferentCandles_Differs()
    {
        var other = MarketDataFixtures.CreateCandles(51);
        Assert.That(BacktestDataset.Hash(other, Config), Is.Not.EqualTo(BacktestDataset.Hash(Candles, Config)));
    }

    [Test]
    public void Hash_IsLowercaseHex()
    {
        var hash = BacktestDataset.Hash(Candles, Config);
        Assert.That(hash, Does.Match("^[0-9a-f]{64}$"));
    }
}
