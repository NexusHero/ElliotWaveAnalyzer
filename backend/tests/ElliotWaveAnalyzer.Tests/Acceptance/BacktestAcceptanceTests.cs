using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure.Auth;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance for the backtest harness: running over the faked BTC history persists an
/// aggregated run, a re-run with the same dataset is idempotent (same hash, no duplicate row), and the
/// summary reads back over HTTP (authenticated). The run itself goes through the real service + engine
/// + EF/PostgreSQL; only the market-data boundary is faked.
/// </summary>
[TestFixture]
public sealed class BacktestAcceptanceTests
{
    // A short window keeps the parser fast while still sliding many cutoffs.
    private static readonly BacktestConfig Config =
        new(WarmupCandles: 150, Step: 30, HorizonCandles: 30, PivotThresholdPercent: 3m);

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

    private async Task<BacktestSummary> RunAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var backtest = scope.ServiceProvider.GetRequiredService<IBacktestService>();
        return await backtest.RunAsync("BTC", Config);
    }

    [Test]
    public async Task Run_PersistsAnAggregatedSummary_ThenSummaryEndpointReturnsIt()
    {
        var summary = await RunAsync();

        var response = await _client.GetAsync("/api/backtest/summary");
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(root.GetProperty("datasetHash").GetString(), Is.EqualTo(summary.DatasetHash));
            Assert.That(root.GetProperty("engineVersion").GetString(), Is.EqualTo("1"));
            Assert.That(root.GetProperty("symbol").GetString(), Is.EqualTo("BTC"));
            Assert.That(root.GetProperty("buckets").ValueKind, Is.EqualTo(JsonValueKind.Array));
        });
    }

    [Test]
    public async Task Rerun_SameDataset_IsIdempotent_NoDuplicateRun()
    {
        var first = await RunAsync();
        var second = await RunAsync();

        Assert.That(second.DatasetHash, Is.EqualTo(first.DatasetHash));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var runsForHash = db.BacktestRuns.Count(r => r.DatasetHash == first.DatasetHash);
        Assert.That(runsForHash, Is.EqualTo(1), "a re-run over the same dataset must not duplicate the run");
    }

    [Test]
    public async Task Summary_Unauthenticated_Returns401()
    {
        using var anon = _factory.CreateClient();

        var response = await anon.GetAsync("/api/backtest/summary");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task RunEndpoint_OutsideDevelopment_Returns404()
    {
        // The acceptance host runs as "Testing", so the dev-guarded run endpoint must hide itself.
        var response = await _client.PostAsJsonAsync("/api/backtest/run", new { symbol = "BTC" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
