using System.Net;
using ElliotWaveAnalyzer.Api.Infrastructure.Auth;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance tests for the liveness/readiness endpoints (#173): liveness is
/// unconditional (AC1), readiness reflects a real, simulated database outage — not a mock — so it
/// is safe for a load balancer/orchestrator to gate traffic on (AC1, AC4).
/// </summary>
[TestFixture]
public sealed class HealthEndpointsAcceptanceTests
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
    public async Task Liveness_AlwaysReturnsHealthy_RegardlessOfDependencies()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Readiness_EverythingReachable_ReturnsHealthy()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Readiness_DatabaseUnreachable_ReturnsServiceUnavailable()
    {
        // A real simulated dependency failure — the database check runs against a genuinely
        // unreachable connection, not a stub that always says "down".
        using var host = _factory.WithWebHostBuilder(builder =>
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

                // Port 1 on loopback: nothing listens there, so every connection attempt fails fast.
                services.AddDbContext<AppDbContext>(o => o.UseNpgsql(
                    "Host=127.0.0.1;Port=1;Username=none;Password=none;Database=none;Timeout=2"));
            }));
        using var client = host.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
    }
}
