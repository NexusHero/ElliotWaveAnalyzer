using ElliotWaveAnalyzer.Api.Infrastructure;
using ElliotWaveAnalyzer.Tests.TestData;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="YahooSymbolResolver"/>. The Yahoo search boundary is stubbed with
/// canned JSON — no network. The same endpoint answers ticker, name and ISIN queries, so one
/// fixture covers all three; only the query differs at the call site.
/// </summary>
[TestFixture]
public sealed class YahooSymbolResolverTests
{
    private const string SearchJson =
        """
        {
          "quotes": [
            { "symbol": "RKLB", "shortname": "Rocket Lab", "longname": "Rocket Lab USA, Inc.",
              "quoteType": "equity", "exchDisp": "NASDAQ" },
            { "symbol": "RKLB.MX", "shortname": "Rocket Lab MX", "quoteType": "equity", "exchDisp": "Mexico" },
            { "shortname": "no symbol — must be skipped", "quoteType": "equity" }
          ]
        }
        """;

    private static YahooSymbolResolver Build(string json) =>
        new(
            StubHttpMessageHandler.JsonClient(json, "https://query1.finance.yahoo.com/"),
            NullLogger<YahooSymbolResolver>.Instance);

    [Test]
    public async Task SearchAsync_MapsQuotes_SkipsSymbolless_BestFirst()
    {
        var results = await Build(SearchJson).SearchAsync("US88160R1014"); // an ISIN query

        Assert.That(results, Has.Count.EqualTo(2)); // the symbol-less quote is skipped
        var first = results[0];
        Assert.Multiple(() =>
        {
            Assert.That(first.Symbol, Is.EqualTo("RKLB"));
            Assert.That(first.Name, Is.EqualTo("Rocket Lab USA, Inc.")); // longname preferred over shortname
            Assert.That(first.AssetClass, Is.EqualTo("EQUITY"));          // upper-cased
            Assert.That(first.Exchange, Is.EqualTo("NASDAQ"));
        });
    }

    [Test]
    public async Task SearchAsync_UsesShortName_WhenLongNameMissing()
    {
        var results = await Build(SearchJson).SearchAsync("RKLB");

        Assert.That(results[1].Name, Is.EqualTo("Rocket Lab MX")); // no longname → shortname
    }

    [Test]
    public async Task SearchAsync_BlankQuery_ReturnsEmpty_WithoutCallingUpstream()
    {
        var results = await Build(SearchJson).SearchAsync("   ");

        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task SearchAsync_NoMatches_ReturnsEmpty()
    {
        var results = await Build("""{ "quotes": [] }""").SearchAsync("zzzznope");

        Assert.That(results, Is.Empty);
    }
}
