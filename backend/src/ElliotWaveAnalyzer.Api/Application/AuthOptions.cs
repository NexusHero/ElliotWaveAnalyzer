namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Authentication configuration. Bound from <c>appsettings.json → Auth</c>.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>How long a session stays valid after login.</summary>
    public int SessionLifetimeHours { get; init; } = 24;

    /// <summary>Name of the session cookie.</summary>
    public string CookieName { get; init; } = "ewa_session";
}
