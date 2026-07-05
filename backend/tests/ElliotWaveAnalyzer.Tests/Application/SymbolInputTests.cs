using ElliotWaveAnalyzer.Api.Application;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Unit tests for the pure <see cref="SymbolInput"/> guards: the search-query and ticker character
/// whitelists that replaced the symbol allow-list as the abuse guard (ADR-022).
/// </summary>
[TestFixture]
public sealed class SymbolInputTests
{
    [TestCase("BTC", true)]
    [TestCase("Rocket Lab", true)]   // queries may contain spaces
    [TestCase("US88160R1014", true)] // ISIN
    [TestCase("", false)]
    [TestCase("   ", false)]
    public void IsValidQuery_ChecksEmptinessAndContent(string query, bool expected)
        => Assert.That(SymbolInput.IsValidQuery(query, 64), Is.EqualTo(expected));

    [Test]
    public void IsValidQuery_ControlCharacter_IsRejected()
        => Assert.That(SymbolInput.IsValidQuery("bad\u0007bell", 64), Is.False); // embedded BEL

    [Test]
    public void IsValidQuery_OverTheCap_IsRejected()
        => Assert.That(SymbolInput.IsValidQuery(new string('a', 65), 64), Is.False);

    [TestCase("BTC", true)]
    [TestCase("rklb", true)]
    [TestCase("^IXIC", true)]
    [TestCase("BRK-B", true)]
    [TestCase("SI=F", true)]
    [TestCase("BTC-USD", true)]
    [TestCase("has space", false)]
    [TestCase("bad$char", false)]
    [TestCase("drop;table", false)]
    [TestCase("", false)]
    public void IsValidSymbol_AllowsTickerCharsOnly(string symbol, bool expected)
        => Assert.That(SymbolInput.IsValidSymbol(symbol), Is.EqualTo(expected));

    [Test]
    public void IsValidSymbol_OverTheCap_IsRejected()
        => Assert.That(SymbolInput.IsValidSymbol(new string('A', 33)), Is.False);
}
