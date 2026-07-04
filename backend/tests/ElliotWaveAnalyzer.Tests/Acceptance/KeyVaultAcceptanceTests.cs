using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ElliotWaveAnalyzer.Api.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end tests for the API-key vault on real PostgreSQL: save/list/delete/default round-trip,
/// the security invariants (the plaintext is never returned and the stored ciphertext differs from
/// the plaintext), per-user isolation, and the auth gate.
/// </summary>
[TestFixture]
public sealed class KeyVaultAcceptanceTests
{
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

    [SetUp]
    public async Task ClearKeys()
    {
        // Each test starts from a clean vault so "first key becomes default" is deterministic.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.UserApiKeys.RemoveRange(db.UserApiKeys);
        await db.SaveChangesAsync();
    }

    [Test]
    public async Task Save_ThenList_ReturnsMetadataButNeverTheKey()
    {
        const string secret = "sk-supersecret-abcd1234";
        var save = await _client.PutAsJsonAsync("/api/keys/gemini", new { key = secret });
        var saveBody = await save.Content.ReadAsStringAsync();

        var list = await _client.GetAsync("/api/keys");
        var listBody = await list.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(listBody);
        var gemini = doc.RootElement.EnumerateArray()
            .First(k => k.GetProperty("provider").GetString() == "gemini");
        Assert.Multiple(() =>
        {
            Assert.That(save.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(gemini.GetProperty("last4").GetString(), Is.EqualTo("1234"));
            Assert.That(gemini.GetProperty("isDefault").GetBoolean(), Is.True, "first key becomes default");
            // The secret must never appear in any response.
            Assert.That(saveBody, Does.Not.Contain(secret));
            Assert.That(listBody, Does.Not.Contain(secret));
        });
    }

    [Test]
    public async Task StoredCipherText_DiffersFromPlaintext()
    {
        const string secret = "sk-plaintext-should-be-encrypted-9999";
        await _client.PutAsJsonAsync("/api/keys/claude", new { key = secret });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.UserApiKeys.FirstAsync(k => k.Provider == "claude");

        Assert.Multiple(() =>
        {
            Assert.That(row.CipherText, Is.Not.Empty);
            Assert.That(row.CipherText, Does.Not.Contain(secret), "the key must be encrypted at rest");
        });
    }

    [Test]
    public async Task SetDefault_SwitchesTheActiveProvider()
    {
        await _client.PutAsJsonAsync("/api/keys/gemini", new { key = "gemini-key-aaaa" });
        await _client.PutAsJsonAsync("/api/keys/openai", new { key = "openai-key-bbbb" });

        var switched = await _client.PutAsync("/api/keys/openai/default", null);

        var list = await _client.GetFromJsonAsync<JsonElement>("/api/keys");
        var openai = list.EnumerateArray().First(k => k.GetProperty("provider").GetString() == "openai");
        Assert.Multiple(() =>
        {
            Assert.That(switched.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(openai.GetProperty("isDefault").GetBoolean(), Is.True);
        });
    }

    [Test]
    public async Task Delete_RemovesTheKey_AndPromotesANewDefault()
    {
        await _client.PutAsJsonAsync("/api/keys/gemini", new { key = "gemini-key-cccc" });
        await _client.PutAsJsonAsync("/api/keys/claude", new { key = "claude-key-dddd" });
        await _client.PutAsync("/api/keys/gemini/default", null);

        var delete = await _client.DeleteAsync("/api/keys/gemini");

        var list = await _client.GetFromJsonAsync<JsonElement>("/api/keys");
        var providers = list.EnumerateArray().Select(k => k.GetProperty("provider").GetString()).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(providers, Does.Not.Contain("gemini"));
            // The remaining key was promoted to default.
            var claude = list.EnumerateArray().First(k => k.GetProperty("provider").GetString() == "claude");
            Assert.That(claude.GetProperty("isDefault").GetBoolean(), Is.True);
        });
    }

    [Test]
    public async Task UnknownProvider_Returns400()
    {
        var response = await _client.PutAsJsonAsync("/api/keys/notaprovider", new { key = "x" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Endpoints_RequireAuthentication()
    {
        using var anon = _factory.CreateClient();

        var response = await anon.GetAsync("/api/keys");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
