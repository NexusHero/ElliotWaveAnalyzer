using System.Net;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance tests for the public marketing pages (#179 AC1, AC3): reachable
/// unauthenticated, carrying SEO/social-preview metadata, and cross-linking each other and the
/// legal pages.
/// </summary>
[TestFixture]
public sealed class MarketingEndpointsAcceptanceTests
{
    private AcceptanceWebApplicationFactory _factory = null!;

    [SetUp]
    public async Task SetUp()
    {
        TestDocker.SkipIfUnavailable();
        _factory = new AcceptanceWebApplicationFactory();
        await _factory.InitializeAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [TestCase("/landing")]
    [TestCase("/pricing")]
    public async Task MarketingRoute_Unauthenticated_Returns200Html(string route)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(route);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));
    }

    [TestCase("/landing")]
    [TestCase("/pricing")]
    public async Task MarketingRoute_CarriesSeoAndSocialPreviewMetadata(string route)
    {
        using var client = _factory.CreateClient();

        var body = await (await client.GetAsync(route)).Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("<title>"));
            Assert.That(body, Does.Contain("name=\"description\""));
            Assert.That(body, Does.Contain("property=\"og:title\""));
            Assert.That(body, Does.Contain("property=\"og:description\""));
        });
    }

    [Test]
    public async Task Landing_LinksToPricingAndTheLegalPages()
    {
        using var client = _factory.CreateClient();

        var body = await (await client.GetAsync("/landing")).Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("href=\"/pricing\""));
            Assert.That(body, Does.Contain("href=\"/legal/impressum\""));
            Assert.That(body, Does.Contain("href=\"/legal/privacy\""));
            Assert.That(body, Does.Contain("href=\"/legal/terms\""));
        });
    }

    [Test]
    public async Task Pricing_PresentsFreeAndPaidTiersWithASignupCtaIntoTheAccountFlow()
    {
        using var client = _factory.CreateClient();

        var body = await (await client.GetAsync("/pricing")).Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("Free"));
            Assert.That(body, Does.Contain("Pro"));
            // The CTA leads into the same SPA the login/register flow lives on.
            Assert.That(body, Does.Contain("href=\"/\">Create free account</a>"));
        });
    }
}
