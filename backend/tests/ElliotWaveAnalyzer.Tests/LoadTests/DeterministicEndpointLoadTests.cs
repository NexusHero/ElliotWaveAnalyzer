using System.Net;
using ElliotWaveAnalyzer.Api.Infrastructure.Auth;
using ElliotWaveAnalyzer.Tests.Acceptance;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ElliotWaveAnalyzer.Tests.LoadTests;

/// <summary>
/// Load/soak harness for the reliability blockers this validates (#170 market-data reliability,
/// #173 health/monitoring), against the deterministic endpoints named in the issue (AC1). Runs
/// in-process against <see cref="AcceptanceWebApplicationFactory"/>'s <c>TestServer</c> —
/// no real network hop, no real deployed instance — so latencies here are an in-process baseline
/// (this app's own request-handling overhead), not a production network number; see ADR-065 for why
/// that's an honest, accepted scope rather than an oversight. <c>[Explicit]</c> so a normal, blocking
/// `dotnet test` run (confirmed empirically: NUnit excludes `[Explicit]` tests from an unfiltered run
/// entirely, not even as "Skipped") never touches this — only the scheduled `load` workflow, which
/// selects it by name, does.
/// </summary>
[TestFixture]
[Explicit("Load/soak — runs on a schedule (.github/workflows/load.yml), not per-PR (#199)")]
public sealed class DeterministicEndpointLoadTests
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

    /// <summary>
    /// AC1: a defined concurrency sustained for a fixed window, asserted against an agreed error-rate
    /// budget and a latency-percentile ceiling — both documented, in-process baselines (ADR-065).
    /// </summary>
    [Test]
    public async Task Scan_UnderSustainedConcurrency_StaysWithinErrorAndLatencyBudget()
    {
        var result = await LoadTestHarness.RunAsync(
            async ct =>
            {
                var response = await _client.GetAsync("/api/scan?symbols=BTC,ETH&timeframe=1d", ct);
                return (int)response.StatusCode;
            },
            concurrency: 20,
            duration: TimeSpan.FromSeconds(20));

        TestContext.Progress.WriteLine($"[load] scan: {result}");

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalRequests, Is.GreaterThan(0), "the harness made no requests at all");
            Assert.That(result.ErrorRatePercent, Is.LessThanOrEqualTo(1.0), "error-rate budget: <=1%");
            Assert.That(result.P95Ms, Is.LessThanOrEqualTo(2_000), "p95 latency ceiling: <=2000ms (in-process baseline)");
        });
    }

    /// <summary>
    /// AC2: a "soak" run — necessarily CI-time-bounded (90s), not an hours-long production soak; that
    /// scope narrowing is stated honestly rather than silently assumed to be the same thing (ADR-065).
    /// Samples managed-heap size at intervals and checks the back half of the run isn't trending
    /// meaningfully above the front half — a coarse but real stability signal, not a full profiler.
    /// </summary>
    [Test]
    public async Task Scan_SustainedOverAWindow_ShowsNoMemoryGrowthTrend()
    {
        var memorySamplesBytes = new List<long>();
        using var sampler = new Timer(
            _ => memorySamplesBytes.Add(GC.GetTotalMemory(forceFullCollection: false)),
            state: null, dueTime: TimeSpan.Zero, period: TimeSpan.FromSeconds(3));

        var result = await LoadTestHarness.RunAsync(
            async ct =>
            {
                var response = await _client.GetAsync("/api/scan?symbols=BTC,ETH&timeframe=1d", ct);
                return (int)response.StatusCode;
            },
            concurrency: 5,
            duration: TimeSpan.FromSeconds(90));

        TestContext.Progress.WriteLine($"[soak] scan: {result}; {memorySamplesBytes.Count} memory samples");

        Assert.That(result.ErrorRatePercent, Is.LessThanOrEqualTo(1.0));

        if (memorySamplesBytes.Count < 6)
        {
            Assert.Inconclusive("Too few memory samples collected to assess a trend.");
        }

        var half = memorySamplesBytes.Count / 2;
        var firstHalfAvg = memorySamplesBytes.Take(half).Average();
        var secondHalfAvg = memorySamplesBytes.Skip(half).Average();

        // A generous ceiling — this proxy isn't trying to catch a slow multi-hour leak, only a gross
        // "requests keep piling memory up" regression (ADR-065).
        Assert.That(secondHalfAvg, Is.LessThanOrEqualTo(firstHalfAvg * 3),
            $"managed heap grew from ~{firstHalfAvg / 1024 / 1024:F1}MB to ~{secondHalfAvg / 1024 / 1024:F1}MB over the run");
    }

    /// <summary>
    /// AC3: readiness flips to unhealthy under concurrent load against a genuinely, simulated
    /// unreachable database — the same DB-swap pattern <c>HealthEndpointsAcceptanceTests</c> already
    /// established for the single-request case, now driven by concurrent callers.
    /// </summary>
    [Test]
    public async Task Readiness_FlipsToNotReady_WhileADependencyFailsUnderConcurrentLoad()
    {
        using var brokenHost = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                var toRemove = services.Where(d =>
                    d.ServiceType == typeof(AppDbContext) ||
                    (d.ServiceType.IsGenericType &&
                     (d.ServiceType.GetGenericTypeDefinition().Name.StartsWith("DbContextOptions", StringComparison.Ordinal) ||
                      d.ServiceType.GetGenericTypeDefinition().Name.StartsWith("IDbContextOptionsConfiguration", StringComparison.Ordinal))) ||
                    d.ServiceType == typeof(DbContextOptions)).ToList();
                foreach (var descriptor in toRemove)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<AppDbContext>(o => o.UseNpgsql(
                    "Host=127.0.0.1;Port=1;Username=none;Password=none;Database=none;Timeout=2"));
            }));
        using var brokenClient = brokenHost.CreateClient();

        var statusCodes = new System.Collections.Concurrent.ConcurrentBag<HttpStatusCode>();
        var result = await LoadTestHarness.RunAsync(
            async ct =>
            {
                var response = await brokenClient.GetAsync("/health/ready", ct);
                statusCodes.Add(response.StatusCode);
                return (int)response.StatusCode;
            },
            concurrency: 10,
            duration: TimeSpan.FromSeconds(5));

        TestContext.Progress.WriteLine($"[load] readiness-under-failure: {result}");

        Assert.That(statusCodes, Is.Not.Empty);
        Assert.That(statusCodes, Has.All.EqualTo(HttpStatusCode.ServiceUnavailable),
            "every readiness probe during the simulated outage must report 503, even under concurrent load");
    }
}
