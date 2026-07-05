using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// <see cref="ScanService"/>: a symbol whose data can't be served is skipped without aborting the
/// sweep, and the good symbols still return hits (scanned counts everyone attempted).
/// </summary>
[TestFixture]
public sealed class ScanServiceTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static IReadOnlyList<MarketCandle> Impulse()
    {
        decimal[] block = [100, 130, 115, 175, 150, 200];
        var turns = new List<decimal>();
        decimal lift = 0m;
        for (var b = 0; b < 4; b++)
        {
            foreach (var p in block)
            {
                turns.Add(p + lift);
            }

            lift += 120m;
        }

        var candles = new List<MarketCandle>();
        var day = 0;
        for (var i = 0; i + 1 < turns.Count; i++)
        {
            for (var s = 0; s < 6; s++)
            {
                var price = turns[i] + ((turns[i + 1] - turns[i]) * (decimal)(s + 1) / 6);
                var prev = candles.Count > 0 ? candles[^1].Close : turns[0];
                candles.Add(new MarketCandle(Start.AddDays(day++), prev, Math.Max(prev, price), Math.Min(prev, price), price, 0m));
            }
        }

        return candles;
    }

    [Test]
    public async Task ScanAsync_OneUnservableSymbol_IsSkipped_OthersStillReturned()
    {
        var technical = Substitute.For<ITechnicalAnalysisService>();
        technical.GetAnalysisAsync("GOOD", Arg.Any<int>(), Arg.Any<CandleInterval>(), Arg.Any<CancellationToken>())
            .Returns(new TechnicalAnalysisResult("GOOD", Impulse(), [], []));
        technical.GetAnalysisAsync("BAD", Arg.Any<int>(), Arg.Any<CandleInterval>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ArgumentException("no data"));

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new ScanService(
            technical, cache, Options.Create(new ScanOptions()), TimeProvider.System,
            NullLogger<ScanService>.Instance);

        var result = await sut.ScanAsync(["GOOD", "BAD"], new ScanFilter(), "1D", limit: 20);

        Assert.Multiple(() =>
        {
            Assert.That(result.Scanned, Is.EqualTo(2), "both were attempted");
            Assert.That(result.Hits, Has.Count.EqualTo(1));
            Assert.That(result.Hits[0].Symbol, Is.EqualTo("GOOD"));
        });
    }

    [Test]
    public async Task ScanAsync_NoSymbols_UsesTheConfiguredDefaultUniverse()
    {
        var technical = Substitute.For<ITechnicalAnalysisService>();
        technical.GetAnalysisAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CandleInterval>(), Arg.Any<CancellationToken>())
            .Returns(new TechnicalAnalysisResult("X", Impulse(), [], []));

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new ScanOptions { DefaultSymbols = ["AAA", "BBB", "CCC"] });
        var sut = new ScanService(technical, cache, options, TimeProvider.System, NullLogger<ScanService>.Instance);

        var result = await sut.ScanAsync(symbols: null, new ScanFilter(), "1D", limit: 20);

        Assert.That(result.Scanned, Is.EqualTo(3));
    }
}
