using ElliotWaveAnalyzer.Api.Domain.Depot;
using ElliotWaveAnalyzer.Api.Infrastructure.DepotImport;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="SmartbrokerPlusPdfImporter"/>. Parses a SYNTHETIC Smartbroker+
/// "Depotübersicht" PDF (fake identity + fake holdings, real column layout — see
/// TestData/Depot/generate_smartbroker_plus_sample.py) so the PdfPig-based parser is exercised
/// against a realistic geometry with no personal data committed.
/// </summary>
[TestFixture]
public sealed class SmartbrokerPlusPdfImporterTests
{
    private static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "Depot", "smartbroker_plus_sample.pdf");

    private static SmartbrokerPlusPdfImporter Build() => new(TimeProvider.System);

    private static DepotImportFile Fixture()
        => new("smartbroker_plus_sample.pdf", "application/pdf", File.ReadAllBytes(FixturePath));

    private static async Task<DepotSnapshot> ImportFixtureAsync()
    {
        var result = await Build().ImportAsync(Fixture());
        Assert.That(result.Success, Is.True, result.Error);
        return result.Snapshot!;
    }

    [Test]
    public void Source_IsSmartbrokerPlus() => Assert.That(Build().Source, Is.EqualTo(BrokerSource.SmartbrokerPlus));

    [Test]
    public void CanHandle_PdfBytes_True_OtherwiseFalse()
    {
        var sut = Build();
        Assert.Multiple(() =>
        {
            Assert.That(sut.CanHandle(Fixture()), Is.True);
            Assert.That(sut.CanHandle(new DepotImportFile("x.csv", "text/csv", "a,b,c"u8.ToArray())), Is.False);
        });
    }

    [Test]
    public async Task Import_ParsesEveryHolding()
    {
        var snapshot = await ImportFixtureAsync();

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Source, Is.EqualTo(BrokerSource.SmartbrokerPlus));
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
            Assert.That(acme.CostValue, Is.EqualTo(1000.00m));
            Assert.That(acme.MarketPrice, Is.EqualTo(120.50m));
            Assert.That(acme.MarketValue, Is.EqualTo(1205.00m));
            Assert.That(acme.GainAbsolute, Is.EqualTo(205.00m));
            Assert.That(acme.GainRelativePercent, Is.EqualTo(20.50m));
            Assert.That(acme.Exchange, Is.EqualTo("XETRA"));
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
    public async Task Import_NonSmartbrokerContent_Fails()
    {
        // A CSV masquerading via a .pdf-ish content type is not a Smartbroker+ statement.
        var result = await Build().ImportAsync(
            new DepotImportFile("mystery.pdf", "application/pdf", "not really a pdf"u8.ToArray()));

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.Not.Null.And.Not.Empty);
    }
}
