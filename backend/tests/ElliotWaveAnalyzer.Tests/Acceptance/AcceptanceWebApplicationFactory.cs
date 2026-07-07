using System.Net.Http.Json;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure.Auth;
using ElliotWaveAnalyzer.Api.Interfaces;
using ElliotWaveAnalyzer.Tests.TestData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// Boots the real API in-memory (full DI graph, routing, serialization, middleware,
/// endpoint and service logic) and only fakes the two external boundaries — the LLM
/// (<see cref="IChatClient"/>) and the market-data source (<see cref="IMarketDataProvider"/>).
///
/// The database is a real, throwaway PostgreSQL container (Testcontainers), so the Npgsql
/// provider and the generated migration SQL are exercised exactly as in production. Call
/// <see cref="InitializeAsync"/> before creating a client; it starts the container and
/// applies migrations. Tests skip when no Docker daemon is reachable (see <see cref="TestDocker"/>).
/// </summary>
public sealed class AcceptanceWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestEmail = "tester@example.com";
    public const string TestPassword = "Str0ng!Passw0rd";

    // An explicit connection string (env ACCEPTANCE_PG_CONNSTRING) targets an existing PostgreSQL —
    // a fresh schema per factory (a random search_path) keeps fixtures isolated. Otherwise a
    // throwaway Testcontainers instance is used, giving full isolation on machines with Docker.
    private static readonly string? ExternalConnectionString =
        Environment.GetEnvironmentVariable("ACCEPTANCE_PG_CONNSTRING");

    private readonly PostgreSqlContainer? _db = ExternalConnectionString is null
        ? new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build()
        : null;

    private readonly string _schema = "acc_" + Guid.NewGuid().ToString("N");

    private string ConnectionString => _db?.GetConnectionString()
        ?? $"{ExternalConnectionString};Search Path={_schema}";

    /// <summary>The faked LLM. Tests can tweak its canned response before calling the API.</summary>
    public FakeChatClient Chat { get; } = new();

    /// <summary>Starts the PostgreSQL container and applies EF migrations against it.</summary>
    public async Task InitializeAsync()
    {
        if (_db is not null)
        {
            await _db.StartAsync();
        }

        using var scope = Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (_db is null)
        {
            await ctx.Database.ExecuteSqlRawAsync($"CREATE SCHEMA IF NOT EXISTS \"{_schema}\";");
        }

        await ctx.Database.MigrateAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }

        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Swap the LLM for a deterministic fake.
            services.RemoveAll<IChatClient>();
            services.AddSingleton<IChatClient>(Chat);

            // Swap the (caching-decorated CoinGecko/Yahoo) market data for deterministic fakes —
            // daily, intraday (1H/4H) and symbol resolution — so no external HTTP is made.
            services.RemoveAll<IMarketDataProvider>();
            services.AddSingleton<IMarketDataProvider, FakeMarketDataProvider>();

            services.RemoveAll<IIntradayMarketDataProvider>();
            services.AddSingleton<IIntradayMarketDataProvider, FakeIntradayMarketDataProvider>();

            services.RemoveAll<ISymbolResolver>();
            services.AddSingleton<ISymbolResolver, FakeSymbolResolver>();

            // Point AppDbContext at the container's PostgreSQL. Remove every EF registration
            // tied to AppDbContext first so the production Npgsql config is fully replaced.
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

            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(ConnectionString));
        });
    }

    /// <summary>
    /// Registers and logs in the standard test user so the client's cookie jar holds a
    /// valid session for subsequent requests to protected endpoints.
    /// </summary>
    public async Task AuthenticateAsync(HttpClient client)
    {
        var credentials = new { email = TestEmail, password = TestPassword, acceptTerms = true };
        await client.PostAsJsonAsync("/api/auth/register", credentials);
        await client.PostAsJsonAsync("/api/auth/login", credentials);
    }
}
