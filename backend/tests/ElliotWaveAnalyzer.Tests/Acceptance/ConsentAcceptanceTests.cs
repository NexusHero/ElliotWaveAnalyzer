using System.Net;
using System.Net.Http.Json;
using ElliotWaveAnalyzer.Api.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance tests for cookie-consent recording (#169 AC5) against real PostgreSQL:
/// anonymous and authenticated capture, validation, and that a consent record tied to an account
/// cascades away with it on deletion (#168).
/// </summary>
[TestFixture]
public sealed class ConsentAcceptanceTests
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

    [Test]
    public async Task RecordConsent_Anonymous_PersistsWithNoUserId()
    {
        using var client = _factory.CreateClient();
        var visitorId = Guid.NewGuid().ToString("N");

        var response = await client.PostAsJsonAsync(
            "/api/consent", new { visitorId, analytics = true, marketing = false, policyVersion = "1" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var record = await db.ConsentRecords.SingleAsync(c => c.VisitorId == visitorId);

        Assert.Multiple(() =>
        {
            Assert.That(record.UserId, Is.Null);
            Assert.That(record.Analytics, Is.True);
            Assert.That(record.Marketing, Is.False);
            Assert.That(record.PolicyVersion, Is.EqualTo("1"));
            Assert.That(record.RecordedAt, Is.GreaterThan(DateTimeOffset.UtcNow.AddMinutes(-5)));
        });
    }

    [Test]
    public async Task RecordConsent_Authenticated_PersistsWithTheCallersUserId()
    {
        using var client = _factory.CreateClient();
        await _factory.AuthenticateAsync(client);
        var me = await client.GetAsync("/api/auth/me");
        var meBody = await me.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var userId = Guid.Parse(meBody.GetProperty("id").GetString()!);

        var visitorId = Guid.NewGuid().ToString("N");
        var response = await client.PostAsJsonAsync(
            "/api/consent", new { visitorId, analytics = false, marketing = true, policyVersion = "1" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var record = await db.ConsentRecords.SingleAsync(c => c.VisitorId == visitorId);

        Assert.That(record.UserId, Is.EqualTo(userId));
    }

    [Test]
    public async Task RecordConsent_MissingVisitorIdOrPolicyVersion_Returns400()
    {
        using var client = _factory.CreateClient();

        var missingVisitorId = await client.PostAsJsonAsync(
            "/api/consent", new { visitorId = "", analytics = false, marketing = false, policyVersion = "1" });
        var missingPolicyVersion = await client.PostAsJsonAsync(
            "/api/consent", new { visitorId = "v1", analytics = false, marketing = false, policyVersion = "" });

        Assert.Multiple(() =>
        {
            Assert.That(missingVisitorId.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(missingPolicyVersion.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
    }

    [Test]
    public async Task DeleteAccount_ConsentRecordTiedToTheAccount_IsCascaded()
    {
        using var client = _factory.CreateClient();
        await _factory.AuthenticateAsync(client);
        var visitorId = Guid.NewGuid().ToString("N");
        await client.PostAsJsonAsync(
            "/api/consent", new { visitorId, analytics = true, marketing = true, policyVersion = "1" });

        var delete = await client.PostAsJsonAsync(
            "/api/auth/delete-account", new { currentPassword = AcceptanceWebApplicationFactory.TestPassword });
        Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.That(await db.ConsentRecords.AnyAsync(c => c.VisitorId == visitorId), Is.False);
    }
}
