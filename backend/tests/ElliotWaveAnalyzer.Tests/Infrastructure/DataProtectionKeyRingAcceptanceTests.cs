using ElliotWaveAnalyzer.Api.Infrastructure.Auth;
using ElliotWaveAnalyzer.Tests.Acceptance;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Proves the Data Protection key ring survives a "restart" and works across independently-built
/// instances (#171, AC1/AC2): a second, wholly independent <see cref="IServiceProvider"/> — built
/// fresh, the same way a new process/instance would be — can decrypt ciphertext produced by the
/// first, as long as both persist their key ring to the same PostgreSQL database. Against the
/// framework's default (local, in-memory-derived) key ring this would fail. Runs against a real
/// database (Testcontainers or <c>ACCEPTANCE_PG_CONNSTRING</c>) since the whole point is exercising
/// actual cross-instance persistence, not an in-memory fake.
/// </summary>
[TestFixture]
public sealed class DataProtectionKeyRingAcceptanceTests
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

        _schema = "dpk_" + Guid.NewGuid().ToString("N");

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

        // Create the schema and run migrations once, up front, via a throwaway provider — both
        // "instances" below then just read/write an already-migrated database, like two real
        // app instances would.
        var migrator = BuildProvider();
        using (var scope = migrator.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (_container is null)
            {
                await db.Database.ExecuteSqlRawAsync($"CREATE SCHEMA IF NOT EXISTS \"{_schema}\";");
            }

            await db.Database.MigrateAsync();
        }

        await migrator.DisposeAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    [Test]
    public async Task CiphertextFromOneInstance_DecryptsOnAnIndependentlyBuiltInstance_SharingOnlyTheDatabase()
    {
        string cipherText;

        // "Instance A" — encrypts, then is fully disposed (simulating a process restart / a
        // different instance shutting down), never reused to decrypt.
        await using (var instanceA = BuildProvider())
        {
            using var scope = instanceA.CreateScope();
            var protector = scope.ServiceProvider
                .GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("ElliotWaveAnalyzer.UserApiKey.v1");
            cipherText = protector.Protect("sk-test-plaintext-api-key");
        }

        // "Instance B" — a wholly independent DI container/key-ring instance, built after A is
        // gone, sharing only the database.
        await using var instanceB = BuildProvider();
        using var scopeB = instanceB.CreateScope();
        var protectorB = scopeB.ServiceProvider
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("ElliotWaveAnalyzer.UserApiKey.v1");

        var decrypted = protectorB.Unprotect(cipherText);

        Assert.That(decrypted, Is.EqualTo("sk-test-plaintext-api-key"));
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_connectionString));
        services.AddDataProtection()
            .PersistKeysToDbContext<AppDbContext>()
            .SetApplicationName("ElliotWaveAnalyzer");
        return services.BuildServiceProvider();
    }
}
