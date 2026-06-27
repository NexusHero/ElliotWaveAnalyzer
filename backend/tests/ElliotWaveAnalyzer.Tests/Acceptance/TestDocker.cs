namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// Detects whether a Docker daemon is reachable so container-backed tests can skip cleanly
/// on machines without Docker (keeping local runs green) while still executing in CI, where
/// a Docker socket is present.
/// </summary>
internal static class TestDocker
{
    public static readonly bool IsAvailable = Detect();

    /// <summary>Marks the current test inconclusive/ignored when Docker is unavailable.</summary>
    public static void SkipIfUnavailable()
    {
        if (!IsAvailable)
        {
            Assert.Ignore("Docker is not available; skipping container-backed test.");
        }
    }

    private static bool Detect()
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
