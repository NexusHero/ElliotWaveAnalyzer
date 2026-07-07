using System.Net;
using System.Net.Http.Json;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Domain.Account;
using ElliotWaveAnalyzer.Api.Domain.Depot;
using ElliotWaveAnalyzer.Api.Infrastructure.Auth;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance tests for the DSGVO/GDPR self-service data rights (#168): export (Art. 20)
/// and account deletion (Art. 17) against a real PostgreSQL database, exercising the FK-cascade
/// configured in <see cref="AppDbContext.OnModelCreating"/>.
/// </summary>
[TestFixture]
public sealed class AccountRightsAcceptanceTests
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

    private static object Credentials => new
    {
        email = AcceptanceWebApplicationFactory.TestEmail,
        password = AcceptanceWebApplicationFactory.TestPassword,
    };

    [Test]
    public async Task Export_ThenDelete_FullLifecycle()
    {
        using var client = _factory.CreateClient();
        await _factory.AuthenticateAsync(client);
        var userId = await CurrentUserIdAsync(client);

        // A second, unrelated account with its own data — proves export/deletion never touch it (AC5).
        const string otherEmail = "other-user@example.com";
        using var otherClient = _factory.CreateClient();
        await otherClient.PostAsJsonAsync(
            "/api/auth/register", new { email = otherEmail, password = "AnotherStr0ng!Pass" });
        var otherLogin = await otherClient.PostAsJsonAsync(
            "/api/auth/login", new { email = otherEmail, password = "AnotherStr0ng!Pass" });
        Assert.That(otherLogin.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var otherUserId = await CurrentUserIdAsync(otherClient);

        await SeedPersonalDataAsync(userId, symbol: "BTC", apiKeyCipherText: "top-secret-ciphertext-value");
        await SeedPersonalDataAsync(otherUserId, symbol: "ETH", apiKeyCipherText: "other-users-secret-ciphertext");

        // --- Export (AC1, AC5) ---
        var exportResponse = await client.GetAsync("/api/auth/export");
        Assert.That(exportResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var rawBody = await exportResponse.Content.ReadAsStringAsync();
        var export = await exportResponse.Content.ReadFromJsonAsync<AccountExport>()
            ?? throw new InvalidOperationException("Export response did not deserialize.");

        Assert.Multiple(() =>
        {
            Assert.That(export.Profile.Email, Is.EqualTo(AcceptanceWebApplicationFactory.TestEmail));
            Assert.That(export.Analyses, Has.Count.EqualTo(1));
            Assert.That(export.Analyses[0].Symbol, Is.EqualTo("BTC"));
            Assert.That(export.Analyses[0].Scenarios, Has.Count.EqualTo(1));
            Assert.That(export.Analyses[0].SwitchEvents, Has.Count.EqualTo(1));
            Assert.That(export.ApiKeys, Has.Count.EqualTo(1));
            Assert.That(export.ApiKeys[0].Provider, Is.EqualTo("gemini"));
            Assert.That(export.ApiKeys[0].Last4, Is.EqualTo("abcd"));
            Assert.That(export.Depots, Has.Count.EqualTo(1));
            Assert.That(export.Depots[0].Positions, Has.Count.EqualTo(1));
            Assert.That(export.LlmUsage, Has.Count.EqualTo(1));
            Assert.That(export.LlmUsage[0].CallCount, Is.EqualTo(3));

            // AC5: never the ciphertext (this user's own, or leaked from the other account), and
            // never the other account's data at all.
            Assert.That(rawBody, Does.Not.Contain("top-secret-ciphertext-value"));
            Assert.That(rawBody, Does.Not.Contain("other-users-secret-ciphertext"));
            Assert.That(rawBody, Does.Not.Contain("ETH"));
            Assert.That(rawBody, Does.Not.Contain(otherEmail));
        });

        // --- Deletion refused without the right confirmation (AC2) ---
        var wrongPassword = await client.PostAsJsonAsync(
            "/api/auth/delete-account", new { currentPassword = "not-the-password" });
        Assert.That(wrongPassword.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        await AssertRowCountAsync(userId, expected: 1); // untouched

        // --- Deletion (AC2, AC3, AC4) ---
        var delete = await client.PostAsJsonAsync(
            "/api/auth/delete-account", new { currentPassword = AcceptanceWebApplicationFactory.TestPassword });
        Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // The now-deleted session is immediately unusable, and future logins with those
        // credentials fail (AC2).
        Assert.That((await client.GetAsync("/api/auth/me")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var loginAfterDelete = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", Credentials);
        Assert.That(loginAfterDelete.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        // No orphaned rows anywhere (AC3), and an audit row records the deletion (AC4).
        await AssertRowCountAsync(userId, expected: 0);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var audits = await db.AccountDeletionAudits.Where(a => a.DeletedUserId == userId).ToListAsync();
            Assert.That(audits, Has.Count.EqualTo(1));
            Assert.That(audits[0].DeletedAt, Is.GreaterThan(DateTimeOffset.UtcNow.AddMinutes(-5)));

            // The other account is completely untouched.
            Assert.That(await db.AnalysisSnapshots.CountAsync(s => s.UserId == otherUserId), Is.EqualTo(1));
            Assert.That(await db.Users.AnyAsync(u => u.Id == otherUserId), Is.True);
        }
    }

    [Test]
    public async Task DeleteAccount_OAuthOnlyUser_SucceedsWithoutAPassword()
    {
        using var scope = _factory.Services.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var session = await auth.ExternalLoginAsync("oauth-user@example.com", emailVerified: true, ip: null, userAgent: null);
        Assert.That(session.Succeeded, Is.True);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"ewa_session={session.Token}");

        // Sanity: the manually-attached cookie really authenticates as that user.
        Assert.That((await client.GetAsync("/api/auth/me")).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var delete = await client.PostAsJsonAsync("/api/auth/delete-account", new { currentPassword = (string?)null });

        Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await client.GetAsync("/api/auth/me")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    private async Task<Guid> CurrentUserIdAsync(HttpClient client)
    {
        var me = await client.GetAsync("/api/auth/me");
        var body = await me.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    private async Task SeedPersonalDataAsync(Guid userId, string symbol, string apiKeyCipherText)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;

        db.AnalysisSnapshots.Add(new AnalysisSnapshot
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Symbol = symbol,
            CreatedAt = now,
            Structure = "Impulse",
            Bullish = true,
            Confidence = "High",
            AlertedOutcome = AnalysisOutcome.Pending,
            Scenarios =
            [
                new AnalysisScenarioRow
                {
                    Id = Guid.NewGuid(),
                    Role = ScenarioRole.Primary,
                    OrderIndex = 0,
                    Label = "Primary",
                    Structure = "Impulse",
                    Bullish = true,
                    Confidence = "High",
                },
            ],
            SwitchEvents =
            [
                new AnalysisSwitchEventRow
                {
                    Id = Guid.NewGuid(),
                    At = now,
                    FromLabel = "Primary",
                    ToLabel = "Alternate",
                    Reason = "Invalidation broke",
                },
            ],
        });

        db.UserApiKeys.Add(new UserApiKey
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Provider = "gemini",
            CipherText = apiKeyCipherText,
            Last4 = "abcd",
            IsDefault = true,
            CreatedAt = now,
        });

        db.SavedDepots.Add(new SavedDepot
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Source = BrokerSource.SmartbrokerPlus,
            ImportedAt = now,
            Currency = "EUR",
            Positions =
            [
                new SavedDepotPosition
                {
                    Id = Guid.NewGuid(),
                    Ordinal = 0,
                    Isin = "US0378331005",
                    Name = "Apple Inc.",
                    Quantity = 10,
                },
            ],
        });

        db.UserLlmUsagePeriods.Add(new UserLlmUsagePeriod
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PeriodStart = now.Date,
            CallCount = 3,
        });

        await db.SaveChangesAsync();
    }

    /// <summary>Counts every table a user's data lives in, asserting they all agree (all-seeded or all-gone).</summary>
    private async Task AssertRowCountAsync(Guid userId, int expected)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var snapshots = await db.AnalysisSnapshots.CountAsync(s => s.UserId == userId);
        var scenarios = await db.Set<AnalysisScenarioRow>()
            .CountAsync(r => db.AnalysisSnapshots.Any(s => s.Id == r.AnalysisSnapshotId && s.UserId == userId));
        var apiKeys = await db.UserApiKeys.CountAsync(k => k.UserId == userId);
        var depots = await db.SavedDepots.CountAsync(d => d.UserId == userId);
        var usagePeriods = await db.UserLlmUsagePeriods.CountAsync(p => p.UserId == userId);
        var identityUser = await db.Users.CountAsync(u => u.Id == userId);

        Assert.Multiple(() =>
        {
            Assert.That(snapshots, Is.EqualTo(expected), "AnalysisSnapshots");
            Assert.That(scenarios, Is.EqualTo(expected), "AnalysisScenarioRows");
            Assert.That(apiKeys, Is.EqualTo(expected), "UserApiKeys");
            Assert.That(depots, Is.EqualTo(expected), "SavedDepots");
            Assert.That(usagePeriods, Is.EqualTo(expected), "UserLlmUsagePeriods");
            Assert.That(identityUser, Is.EqualTo(expected), "AspNetUsers");
        });
    }
}
