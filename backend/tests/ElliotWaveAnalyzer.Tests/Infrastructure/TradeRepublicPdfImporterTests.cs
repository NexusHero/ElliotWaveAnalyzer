using ElliotWaveAnalyzer.Api.Domain.Depot;
using ElliotWaveAnalyzer.Api.Infrastructure.DepotImport;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="TradeRepublicPdfImporter"/>. Parses a SYNTHETIC Trade Republic
/// "Depotübersicht" PDF (fake identity + fake holdings, single-row-per-position layout — see
/// TestData/Depot/generate_trade_republic_sample.py) so the PdfPig-based parser is exercised against
/// a realistic geometry with no personal data committed (#113).
/// </summary>
[TestFixture]
public sealed class TradeRepublicPdfImporterTests
{
    private static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "Depot", "trade_republic_sample.pdf");

    private static readonly string SmartbrokerFixturePath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "Depot", "smartbroker_plus_sample.pdf");

    private static TradeRepublicPdfImporter Build() => new(TimeProvider.System);

    private static DepotImportFile Fixture()
        => new("trade_republic_sample.pdf", "application/pdf", File.ReadAllBytes(FixturePath));

    private static async Task<DepotSnapshot> ImportFixtureAsync()
    {
        var result = await Build().ImportAsync(Fixture());
        Assert.That(result.Success, Is.True, result.Error);
        return result.Snapshot!;
    }

    [Test]
    public void Source_IsTradeRepublic() => Assert.That(Build().Source, Is.EqualTo(BrokerSource.TradeRepublic));

    [Test]
    public void CanHandle_TradeRepublicPdf_True_OtherwiseFalse()
    {
        var sut = Build();
        Assert.Multiple(() =>
        {
            Assert.That(sut.CanHandle(Fixture()), Is.True);
            Assert.That(sut.CanHandle(new DepotImportFile("x.csv", "text/csv", "a,b,c"u8.ToArray())), Is.False);
        });
    }

    [Test]
    public void CanHandle_ASmartbrokerPlusPdf_IsFalse_TheTwoPdfImportersDoNotClash()
    {
        // AC: "CanHandle recognises it and doesn't clash with the Smartbroker+/Scalable importers."
        // Both are PDFs, so CanHandle must be content-aware, not just "is this a PDF".
        var sut = Build();
        var smartbroker = new DepotImportFile(
            "smartbroker_plus_sample.pdf", "application/pdf", File.ReadAllBytes(SmartbrokerFixturePath));

        Assert.That(sut.CanHandle(smartbroker), Is.False);
    }

    [Test]
    public void SmartbrokerImporter_CanHandle_ATradeRepublicPdf_IsFalse_TheTwoPdfImportersDoNotClashEitherWay()
    {
        var smartbrokerImporter = new SmartbrokerPlusPdfImporter(TimeProvider.System);
        Assert.That(smartbrokerImporter.CanHandle(Fixture()), Is.False);
    }

    [Test]
    public async Task Import_ParsesEveryHolding()
    {
        var snapshot = await ImportFixtureAsync();

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Source, Is.EqualTo(BrokerSource.TradeRepublic));
            Assert.That(snapshot.Currency, Is.EqualTo("EUR"));
            Assert.That(snapshot.Positions, Has.Count.EqualTo(3));
            Assert.That(snapshot.Positions.Select(p => p.Isin),
                Is.EqualTo(new[] { "US0000000001", "DE0000000002", "NL0000000003" }));
        });
    }

    [Test]
    public async Task Import_FirstPosition_HasAllFields()
    {
        var acme = (await ImportFixtureAsync()).Positions[0];

        Assert.Multiple(() =>
        {
            Assert.That(acme.Isin, Is.EqualTo("US0000000001"));
            Assert.That(acme.Name, Is.EqualTo("ACME Robotics Inc."));
            Assert.That(acme.Quantity, Is.EqualTo(10m));
            Assert.That(acme.CostPrice, Is.EqualTo(100.00m));
            Assert.That(acme.MarketPrice, Is.EqualTo(120.50m));
            Assert.That(acme.MarketValue, Is.EqualTo(1205.00m));
            Assert.That(acme.GainAbsolute, Is.EqualTo(205.00m));
            Assert.That(acme.GainRelativePercent, Is.EqualTo(20.50m));
        });
    }

    [Test]
    public async Task Import_ParsesNegativeGainLoss()
    {
        var beispiel = (await ImportFixtureAsync()).Positions[1];

        Assert.Multiple(() =>
        {
            Assert.That(beispiel.GainAbsolute, Is.EqualTo(-30.00m));
            Assert.That(beispiel.GainRelativePercent, Is.EqualTo(-12.00m));
            Assert.That(beispiel.MarketValue, Is.EqualTo(220.00m));
        });
    }

    [Test]
    public async Task Import_ParsesDepotTotalsAndExportTimestamp()
    {
        var snapshot = await ImportFixtureAsync();

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Totals, Is.Not.Null);
            Assert.That(snapshot.Totals!.TotalValue, Is.EqualTo(2055.00m));
            Assert.That(snapshot.Totals.GainAbsolute, Is.EqualTo(205.00m));
            Assert.That(snapshot.Totals.GainRelativePercent, Is.EqualTo(11.08m));
            Assert.That(snapshot.ExportedAt!.Value.UtcDateTime,
                Is.EqualTo(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
        });
    }

    [Test]
    public async Task Import_NonTradeRepublicContent_Fails()
    {
        var result = await Build().ImportAsync(
            new DepotImportFile("mystery.pdf", "application/pdf", "not really a pdf"u8.ToArray()));

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.Not.Null.And.Not.Empty);
    }
}
