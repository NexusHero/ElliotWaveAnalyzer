using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance tests for <c>POST /api/depot/import</c>. Drives the running API over
/// HTTP with a multipart upload of the synthetic Smartbroker+ fixture, exercising routing,
/// authentication, the multipart binding and the real PdfPig parser.
/// </summary>
[TestFixture]
public sealed class DepotImportAcceptanceTests
{
    private static readonly string FixturePath =
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
        var file = new ByteArrayContent(File.ReadAllBytes(FixturePath));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return new MultipartFormDataContent { { file, "file", "smartbroker_plus_sample.pdf" } };
    }

    [Test]
    public async Task ImportDepot_SmartbrokerPdf_Returns200WithParsedHoldings()
    {
        var response = await _client.PostAsync("/api/depot/import", PdfUpload());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("currency").GetString(), Is.EqualTo("EUR"));
            Assert.That(body.GetProperty("positions").GetArrayLength(), Is.EqualTo(3));
            Assert.That(body.GetProperty("positions")[0].GetProperty("isin").GetString(),
                Is.EqualTo("US0000000001"));
            Assert.That(body.GetProperty("totals").GetProperty("totalValue").GetDecimal(),
                Is.EqualTo(2055.00m));
        });
    }

    [Test]
    public async Task ImportDepot_EmptyUpload_Returns400()
    {
        using var empty = new MultipartFormDataContent
        {
            { new ByteArrayContent([]), "file", "empty.pdf" },
        };

        var response = await _client.PostAsync("/api/depot/import", empty);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task ImportDepot_UnsupportedFile_Returns400()
    {
        var csv = new ByteArrayContent("Name,ISIN,Qty\nACME,US0000000001,10"u8.ToArray());
        csv.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        using var upload = new MultipartFormDataContent { { csv, "file", "holdings.csv" } };

        var response = await _client.PostAsync("/api/depot/import", upload);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task ImportDepot_Unauthenticated_Returns401()
    {
        using var anon = _factory.CreateClient();

        var response = await anon.PostAsync("/api/depot/import", PdfUpload());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
