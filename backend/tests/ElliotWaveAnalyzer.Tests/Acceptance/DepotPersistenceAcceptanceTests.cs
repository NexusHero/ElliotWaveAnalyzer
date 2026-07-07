using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance tests for depot persistence: an import is saved for the user and read
/// back via <c>GET /api/depot</c>; every import accumulates as a snapshot in the history (#115)
/// rather than replacing the previous one. Drives the running API over HTTP against a real
/// PostgreSQL (Testcontainers).
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
    public async Task Reimport_LatestImportWins_ButBothSurviveInHistory()
    {
        // First a PDF (3 holdings), then a CSV (1 aggregated holding) — GET /api/depot always
        // shows the latest, but #115 means the earlier one is not deleted (checked via the
        // history's delta, since this fixture's user accumulates imports across test methods).
        var beforeCount = (await HistoryAsync()).GetArrayLength();

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

        var afterHistory = await HistoryAsync();
        Assert.Multiple(() =>
        {
            Assert.That(afterHistory.GetArrayLength(), Is.EqualTo(beforeCount + 2));
            // Newest first: the CSV import we just made, then the PDF import just before it.
            Assert.That(afterHistory[0].GetProperty("source").GetString(), Is.EqualTo("ScalableCapital"));
            Assert.That(afterHistory[1].GetProperty("source").GetString(), Is.EqualTo("SmartbrokerPlus"));
        });
    }

    [Test]
    public async Task GetHistory_ThenFetchById_ReturnsThatSnapshotsFullHoldings()
    {
        await _client.PostAsync("/api/depot/import", PdfUpload());

        var history = await HistoryAsync();
        var latestId = history[0].GetProperty("id").GetGuid();

        var response = await _client.GetAsync($"/api/depot/history/{latestId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("source").GetString(), Is.EqualTo("SmartbrokerPlus"));
            Assert.That(body.GetProperty("positions").GetArrayLength(), Is.EqualTo(3));
        });
    }

    [Test]
    public async Task GetSnapshotById_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/api/depot/history/{Guid.NewGuid()}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetSnapshotById_AnotherUsersSnapshot_Returns404_NotTheirData()
    {
        await _client.PostAsync("/api/depot/import", PdfUpload());
        var mine = (await HistoryAsync())[0].GetProperty("id").GetGuid();

        using var otherUser = _factory.CreateClient();
        await otherUser.PostAsJsonAsync("/api/auth/register", new { email = "other-depot-user@example.com", password = "Str0ng!Passw0rd" });
        await otherUser.PostAsJsonAsync("/api/auth/login", new { email = "other-depot-user@example.com", password = "Str0ng!Passw0rd" });

        var response = await otherUser.GetAsync($"/api/depot/history/{mine}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetDepot_Unauthenticated_Returns401()
    {
        using var anon = _factory.CreateClient();

        var response = await anon.GetAsync("/api/depot");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GetHistory_Unauthenticated_Returns401()
    {
        using var anon = _factory.CreateClient();

        var response = await anon.GetAsync("/api/depot/history");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    private async Task<JsonElement> HistoryAsync()
    {
        var response = await _client.GetAsync("/api/depot/history");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
