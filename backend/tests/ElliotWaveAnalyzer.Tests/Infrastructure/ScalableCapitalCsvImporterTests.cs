using System.Text;
using ElliotWaveAnalyzer.Api.Domain.Depot;
using ElliotWaveAnalyzer.Api.Infrastructure.DepotImport;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="ScalableCapitalCsvImporter"/>: it aggregates a Scalable Capital
/// transactions CSV into current holdings (net shares per ISIN, average cost), drops fully-sold
/// positions, and ignores non-trade rows. Fixtures are synthetic inline CSV — no real data.
/// </summary>
[TestFixture]
public sealed class ScalableCapitalCsvImporterTests
{
    private const string Header =
        "date;time;status;reference;description;assetType;type;isin;shares;price;amount;fee;tax;currency";

    private static ScalableCapitalCsvImporter Build() => new(TimeProvider.System);

    private static DepotImportFile Csv(string body)
        => new("transactions.csv", "text/csv", Encoding.UTF8.GetBytes(body));

    private static string Sheet(params string[] rows) => Header + "\n" + string.Join("\n", rows);

    private static async Task<DepotSnapshot> ImportAsync(string body)
    {
        var result = await Build().ImportAsync(Csv(body));
        Assert.That(result.Success, Is.True, result.Error);
        return result.Snapshot!;
    }

    [Test]
    public void Source_IsScalableCapital() => Assert.That(Build().Source, Is.EqualTo(BrokerSource.ScalableCapital));

    [Test]
    public void CanHandle_ScalableCsv_True_PdfAndOtherCsv_False()
    {
        var sut = Build();
        Assert.Multiple(() =>
        {
            Assert.That(sut.CanHandle(Csv(Sheet())), Is.True);
            Assert.That(sut.CanHandle(new DepotImportFile("x.pdf", "application/pdf", "%PDF-1.7"u8.ToArray())), Is.False);
            Assert.That(sut.CanHandle(new DepotImportFile("other.csv", "text/csv", "a;b;c\n1;2;3"u8.ToArray())), Is.False);
        });
    }

    [Test]
    public async Task Import_AggregatesBuysAndSells_NetSharesAndAverageCost()
    {
        // Two buys of ACME (10 @ ~100, 10 @ ~120) then a sell of 5 → net 15; average cost 110.
        var snapshot = await ImportAsync(Sheet(
            "2026-01-01;10:00:00;executed;R1;ACME Robotics;stock;Buy;US0000000001;10;100,00;1000,00;0;0;EUR",
            "2026-01-02;10:00:00;executed;R2;ACME Robotics;stock;Buy;US0000000001;10;120,00;1200,00;0;0;EUR",
            "2026-01-03;10:00:00;executed;R3;ACME Robotics;stock;Sell;US0000000001;5;130,00;650,00;0;0;EUR"));

        Assert.That(snapshot.Positions, Has.Count.EqualTo(1));
        var acme = snapshot.Positions[0];
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Source, Is.EqualTo(BrokerSource.ScalableCapital));
            Assert.That(snapshot.Currency, Is.EqualTo("EUR"));
            Assert.That(acme.Isin, Is.EqualTo("US0000000001"));
            Assert.That(acme.Name, Is.EqualTo("ACME Robotics"));
            Assert.That(acme.Quantity, Is.EqualTo(15m));
            Assert.That(acme.CostPrice, Is.EqualTo(110m)); // (1000+1200)/20
            Assert.That(acme.CostValue, Is.EqualTo(1650m)); // 110 * 15
            Assert.That(acme.MarketValue, Is.Null); // transactions carry no current market price
        });
    }

    [Test]
    public async Task Import_DropsFullySoldPositions_AndIgnoresNonTradeRows()
    {
        var snapshot = await ImportAsync(Sheet(
            "2026-01-01;10:00:00;executed;R1;Beispiel AG;stock;Buy;DE0000000002;8;50,00;400,00;0;0;EUR",
            "2026-01-05;10:00:00;executed;R2;Beispiel AG;stock;Sell;DE0000000002;8;60,00;480,00;0;0;EUR",
            "2026-01-06;10:00:00;executed;R3;Beispiel AG;stock;Dividend;DE0000000002;0;0;12,00;0;0;EUR",
            "2026-01-07;10:00:00;executed;R4;Muster NV;stock;Buy;NL0000000003;3;200,00;600,00;0;0;EUR"));

        Assert.Multiple(() =>
        {
            // DE0000000002 netted to zero → dropped; only the Muster buy remains.
            Assert.That(snapshot.Positions.Select(p => p.Isin), Is.EqualTo(new[] { "NL0000000003" }));
            Assert.That(snapshot.Positions[0].Quantity, Is.EqualTo(3m));
        });
    }

    [Test]
    public async Task Import_SkipsCancelledRows()
    {
        var snapshot = await ImportAsync(Sheet(
            "2026-01-01;10:00:00;executed;R1;ACME;stock;Buy;US0000000001;10;100,00;1000,00;0;0;EUR",
            "2026-01-02;10:00:00;cancelled;R2;ACME;stock;Buy;US0000000001;99;100,00;9900,00;0;0;EUR"));

        Assert.That(snapshot.Positions[0].Quantity, Is.EqualTo(10m), "cancelled buy must be ignored");
    }

    [Test]
    public async Task Import_NotAScalableCsv_Fails()
    {
        var result = await Build().ImportAsync(
            new DepotImportFile("notes.csv", "text/csv", "hello,world\n1,2"u8.ToArray()));

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.Not.Null.And.Not.Empty);
    }
}
