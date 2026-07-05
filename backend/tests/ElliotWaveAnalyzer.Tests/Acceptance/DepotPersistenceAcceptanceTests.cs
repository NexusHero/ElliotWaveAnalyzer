using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance tests for depot persistence: an import is saved for the user and read
/// back via <c>GET /api/depot</c>, and a new import replaces the previous one. Drives the running
/// API over HTTP against a real PostgreSQL (Testcontainers).
/// </summary>
[TestFixture]
public sealed class DepotPersistenceAcceptanceTests
{
    private static readonly string PdfFixture =
        Path.Combine(AppContext.BaseDirectory, "TestData", "Depot", "smartbroker_plus_sample.pdf");

    private AcceptanceWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        TestDocker.SkipIfUnavailable();
        _factory = new AcceptanceWebApplicationFactory();
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();
        await _factory.AuthenticateAsync(_client);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    private static MultipartFormDataContent PdfUpload()
    {
        var file = new ByteArrayContent(File.ReadAllBytes(PdfFixture));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return new MultipartFormDataContent { { file, "file", "smartbroker_plus_sample.pdf" } };
    }

    private static MultipartFormDataContent CsvUpload()
    {
        const string csv =
            "date;time;status;reference;description;assetType;type;isin;shares;price;amount;fee;tax;currency\n" +
            "2026-01-01;10:00:00;executed;R1;ACME Robotics;stock;Buy;US0000000001;10;100,00;1000,00;0;0;EUR\n" +
            "2026-01-02;10:00:00;executed;R2;ACME Robotics;stock;Sell;US0000000001;4;120,00;480,00;0;0;EUR";
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        content.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        return new MultipartFormDataContent { { content, "file", "transactions.csv" } };
    }

    [Test]
    public async Task ImportPdf_ThenGet_ReturnsSavedHoldings()
    {
        var import = await _client.PostAsync("/api/depot/import", PdfUpload());
        Assert.That(import.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var get = await _client.GetAsync("/api/depot");
        Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await get.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("source").GetString(), Is.EqualTo("SmartbrokerPlus"));
            Assert.That(body.GetProperty("positions").GetArrayLength(), Is.EqualTo(3));
            Assert.That(body.GetProperty("positions")[0].GetProperty("isin").GetString(),
                Is.EqualTo("US0000000001"));
        });
    }

    [Test]
    public async Task Reimport_ReplacesThePreviousDepot()
    {
        // First a PDF (3 holdings), then a CSV (1 aggregated holding) — the latest wins.
        await _client.PostAsync("/api/depot/import", PdfUpload());
        await _client.PostAsync("/api/depot/import", CsvUpload());

        var get = await _client.GetAsync("/api/depot");
        var body = await get.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("source").GetString(), Is.EqualTo("ScalableCapital"));
            Assert.That(body.GetProperty("positions").GetArrayLength(), Is.EqualTo(1));
            Assert.That(body.GetProperty("positions")[0].GetProperty("quantity").GetDecimal(), Is.EqualTo(6m));
        });
    }

    [Test]
    public async Task GetDepot_Unauthenticated_Returns401()
    {
        using var anon = _factory.CreateClient();

        var response = await anon.GetAsync("/api/depot");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
