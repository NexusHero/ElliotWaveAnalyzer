using ElliotWaveAnalyzer.Api.Infrastructure.Auth;
using ElliotWaveAnalyzer.Api.Infrastructure.Llm;
using ElliotWaveAnalyzer.Api.Interfaces;
using ElliotWaveAnalyzer.Tests.Acceptance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// <see cref="UserLlmQuotaService"/> against real PostgreSQL: the atomic consume, the (N+1)-th
/// refusal (#174, AC5), and persistence across an independently-built instance (AC3 — a restart
/// or another node sharing the same database sees the same count).
/// </summary>
[TestFixture]
public sealed class UserLlmQuotaServiceAcceptanceTests
{
    private static string? AcceptancePgConnString =>
        Environment.GetEnvironmentVariable("ACCEPTANCE_PG_CONNSTRING");

    private PostgreSqlContainer? _container;
    private string _schema = null!;
    private string _connectionString = null!;

    [SetUp]
    public async Task SetUp()
    {
        TestDocker.SkipIfUnavailable();
        _schema = "quota_" + Guid.NewGuid().ToString("N");
        if (AcceptancePgConnString is { } external)
        {
            _connectionString = $"{external};Search Path={_schema}";
        }
        else
        {
            _container = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();
        }

        await using var migrator = BuildProvider(TimeProvider.System, maxCallsPerPeriod: 3);
        using var scope = migrator.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (_container is null)
        {
            await db.Database.ExecuteSqlRawAsync($"CREATE SCHEMA IF NOT EXISTS \"{_schema}\";");
        }
        await db.Database.MigrateAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    [Test]
    public async Task TryConsumeAsync_NthCallAllowed_NPlusOnethCallRefused()
    {
        var userId = Guid.NewGuid();
        await using var provider = BuildProvider(TimeProvider.System, maxCallsPerPeriod: 3);
        using var scope = provider.CreateScope();
        var quota = scope.ServiceProvider.GetRequiredService<IUserLlmQuotaService>();

        var results = new List<bool>();
        for (var i = 0; i < 4; i++)
        {
            results.Add(await quota.TryConsumeAsync(userId));
        }

        Assert.That(results, Is.EqualTo(new[] { true, true, true, false }));
    }

    [Test]
    public async Task GetStatusAsync_ReflectsConsumedCallsAndTheConfiguredLimit()
    {
        var userId = Guid.NewGuid();
        await using var provider = BuildProvider(TimeProvider.System, maxCallsPerPeriod: 3);
        using var scope = provider.CreateScope();
        var quota = scope.ServiceProvider.GetRequiredService<IUserLlmQuotaService>();

        await quota.TryConsumeAsync(userId);
        await quota.TryConsumeAsync(userId);
        var status = await quota.GetStatusAsync(userId);

        Assert.Multiple(() =>
        {
            Assert.That(status.UsedCalls, Is.EqualTo(2));
            Assert.That(status.Limit, Is.EqualTo(3));
            Assert.That(status.IsExceeded, Is.False);
        });
    }

    [Test]
    public async Task TryConsumeAsync_UsageFromOneInstance_IsVisibleOnAnIndependentlyBuiltInstance()
    {
        var userId = Guid.NewGuid();
        await using (var instanceA = BuildProvider(TimeProvider.System, maxCallsPerPeriod: 1))
        {
            using var scope = instanceA.CreateScope();
            var quota = scope.ServiceProvider.GetRequiredService<IUserLlmQuotaService>();
            Assert.That(await quota.TryConsumeAsync(userId), Is.True);
        }

        await using var instanceB = BuildProvider(TimeProvider.System, maxCallsPerPeriod: 1);
        using var scopeB = instanceB.CreateScope();
        var quotaB = scopeB.ServiceProvider.GetRequiredService<IUserLlmQuotaService>();

        // Same period, same limit already spent on instance A — instance B, sharing only the
        // database, must see the same exhausted quota (AC3: survives a restart / another node).
        Assert.That(await quotaB.TryConsumeAsync(userId), Is.False);
    }

    [Test]
    public async Task TryConsumeAsync_NextPeriod_ResetsTheCount()
    {
        var userId = Guid.NewGuid();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero));
        await using var provider = BuildProvider(clock, maxCallsPerPeriod: 1, periodDays: 1);
        using var scope = provider.CreateScope();
        var quota = scope.ServiceProvider.GetRequiredService<IUserLlmQuotaService>();

        Assert.That(await quota.TryConsumeAsync(userId), Is.True);
        Assert.That(await quota.TryConsumeAsync(userId), Is.False);

        clock.Advance(TimeSpan.FromDays(1));

        Assert.That(await quota.TryConsumeAsync(userId), Is.True);
    }

    private ServiceProvider BuildProvider(TimeProvider timeProvider, int maxCallsPerPeriod, int periodDays = 1)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(timeProvider);
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_connectionString));
        services.AddScoped<IUserLlmQuotaService, UserLlmQuotaService>();
        services.AddSingleton(Options.Create(
            new LlmQuotaOptions { MaxCallsPerPeriod = maxCallsPerPeriod, PeriodDays = periodDays }));
        return services.BuildServiceProvider();
    }

    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
