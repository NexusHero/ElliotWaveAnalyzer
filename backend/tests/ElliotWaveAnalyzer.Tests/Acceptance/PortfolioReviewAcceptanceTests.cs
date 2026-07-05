using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance for the portfolio review (<c>GET /api/depot/analysis</c>): an imported depot
/// is reviewed over the full stack (auth, EF/PostgreSQL, ISIN resolution, the analysis pipeline). With
/// the deterministic fakes the fixture's holdings resolve to an instrument the market-data fakes don't
/// serve, so they surface as <c>unresolved</c> with a clear reason — exercising the "never a silent
/// gap" contract — while the endpoint returns a well-formed review + summary. Unauthenticated → 401.
/// </summary>
[TestFixture]
public sealed class PortfolioReviewAcceptanceTests
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

    [Test]
    public async Task GetAnalysis_ForImportedDepot_ReturnsAReviewWithASummary()
    {
        var import = await _client.PostAsync("/api/depot/import", PdfUpload());
        Assert.That(import.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var response = await _client.GetAsync("/api/depot/analysis");
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(root.TryGetProperty("briefs", out _), Is.True);
            Assert.That(root.TryGetProperty("unresolved", out var unresolved), Is.True);
            var summary = root.GetProperty("summary");
            Assert.That(summary.GetProperty("positions").GetInt32(), Is.GreaterThan(0));
            // The fixture's holdings can't be served by the market-data fakes, so each is surfaced
            // explicitly with a reason rather than dropped.
            Assert.That(unresolved.GetArrayLength(), Is.GreaterThan(0));
            Assert.That(unresolved[0].GetProperty("reason").GetString(), Is.Not.Empty);
        });
    }

    [Test]
    public async Task GetAnalysis_Unauthenticated_Returns401()
    {
        using var anon = _factory.CreateClient();

        var response = await anon.GetAsync("/api/depot/analysis");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
