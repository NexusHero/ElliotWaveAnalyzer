using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Infrastructure.Auth;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Integration test against a real PostgreSQL instance. It applies the EF migration and
/// runs a register → login → validate round-trip, so the Npgsql provider and the generated
/// migration SQL are actually exercised (the in-memory tests cannot catch those).
///
/// Skipped unless the <c>EWA_TEST_POSTGRES</c> connection string is set, so local runs
/// without a database stay green; CI sets it via a PostgreSQL service container.
/// </summary>
[TestFixture]
[Category("Integration")]
public sealed class PostgresAuthIntegrationTests
{
    private const string ConnectionStringEnvVar = "EWA_TEST_POSTGRES";

    [Test]
    public async Task Migration_And_AuthRoundTrip_RunOnRealPostgres()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Ignore($"Set {ConnectionStringEnvVar} to a PostgreSQL connection string to run this test.");
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));
        services.Configure<AuthOptions>(_ => { });
        services
            .AddIdentityCore<AppUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 12;
            })
            .AddEntityFrameworkStores<AppDbContext>();
        services.AddScoped<IAuthService, AuthService>();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var email = $"pg-{Guid.NewGuid():N}@example.com";

        var register = await auth.RegisterAsync(email, "Str0ng!Passw0rd");
        Assert.That(register.Succeeded, Is.True);

        var login = await auth.LoginAsync(email, "Str0ng!Passw0rd", ip: null, userAgent: null);
        Assert.That(login.Succeeded, Is.True);

        var principal = await auth.ValidateSessionAsync(login.Token!);
        Assert.That(principal, Is.Not.Null);
        Assert.That(principal!.Email, Is.EqualTo(email));
    }
}
