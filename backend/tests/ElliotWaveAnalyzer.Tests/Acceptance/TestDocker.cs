namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// Gates database-backed tests. Most run through <see cref="AcceptanceWebApplicationFactory"/>,
/// which uses Testcontainers by default but can target an existing PostgreSQL via the
/// <c>ACCEPTANCE_PG_CONNSTRING</c> env var — so those tests run whenever <em>either</em> Docker or
/// an external database is available (<see cref="SkipIfUnavailable"/>). A few tests spin up their
/// own container directly and therefore need Docker specifically (<see cref="SkipUnlessDockerAvailable"/>).
/// </summary>
internal static class TestDocker
{
    /// <summary>True when Docker is reachable.</summary>
    public static readonly bool DockerAvailable = DetectDocker();

    /// <summary>True when a database is reachable — Docker or an explicit connection string.</summary>
    public static readonly bool IsAvailable = DockerAvailable
        || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ACCEPTANCE_PG_CONNSTRING"));

    /// <summary>Skips when no database (Docker or external Postgres) is reachable.</summary>
    public static void SkipIfUnavailable()
    {
        if (!IsAvailable)
        {
            Assert.Ignore("No database available (need Docker or ACCEPTANCE_PG_CONNSTRING); skipping.");
        }
    }

    /// <summary>Skips when Docker specifically is unavailable (for tests that start their own container).</summary>
    public static void SkipUnlessDockerAvailable()
    {
        if (!DockerAvailable)
        {
            Assert.Ignore("Docker is not available; skipping container-backed test.");
        }
    }

    private static bool DetectDocker()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_HOST")))
        {
            return true;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] sockets =
        [
            "/var/run/docker.sock",                          // Linux / CI / Docker Desktop symlink
            Path.Combine(home, ".docker/run/docker.sock"),   // macOS Docker Desktop
            Path.Combine(home, ".colima/default/docker.sock"), // Colima
            Path.Combine(home, ".rd/docker.sock"),            // Rancher Desktop
        ];

        // Path.Exists (not File.Exists) is required here: a Docker endpoint is a Unix domain
        // socket, and File.Exists returns false for sockets.
        return sockets.Any(Path.Exists);
    }
}
