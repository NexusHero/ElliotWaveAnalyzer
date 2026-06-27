using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> to build the model for migrations
/// without starting the app. The connection string is a placeholder — migrations only
/// need the provider to generate SQL, not a live database.
/// </summary>
internal sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=ewa;Username=postgres;Password=postgres")
            .Options;
        return new AppDbContext(options);
    }
}
