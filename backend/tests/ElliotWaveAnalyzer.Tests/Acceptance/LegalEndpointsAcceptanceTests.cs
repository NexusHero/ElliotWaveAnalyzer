using System.Net;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance tests for the legal pages (#167 AC1, AC5): reachable unauthenticated,
/// and each carries the version/effective-date it claims (AC4).
/// </summary>
[TestFixture]
public sealed class LegalEndpointsAcceptanceTests
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

    [TestCase("/legal/impressum")]
    [TestCase("/legal/privacy")]
    [TestCase("/legal/terms")]
    public async Task LegalRoute_Unauthenticated_Returns200Html(string route)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(route);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));
    }

    [Test]
    public async Task Privacy_CarriesItsVersionAndEffectiveDate()
    {
        using var client = _factory.CreateClient();

        var body = await (await client.GetAsync("/legal/privacy")).Content.ReadAsStringAsync();

        Assert.That(body, Does.Contain(LegalDocuments.PrivacyVersion));
        Assert.That(body, Does.Contain(LegalDocuments.PrivacyEffectiveDate));
    }

    [Test]
    public async Task Terms_CarriesItsVersionAndEffectiveDateAndTheNotAdviceStance()
    {
        using var client = _factory.CreateClient();

        var body = await (await client.GetAsync("/legal/terms")).Content.ReadAsStringAsync();

        Assert.That(body, Does.Contain(LegalDocuments.TermsVersion));
        Assert.That(body, Does.Contain(LegalDocuments.TermsEffectiveDate));
        Assert.That(body, Does.Contain("not investment advice").IgnoreCase);
    }
}
