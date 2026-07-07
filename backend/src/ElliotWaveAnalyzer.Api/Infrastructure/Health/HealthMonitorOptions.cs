using System.ComponentModel.DataAnnotations;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Health;

/// <summary>
/// Configuration for the sustained-downtime alert monitor (#173 AC3). Bound from
/// <c>appsettings.json → HealthMonitor</c>. Inert unless <see cref="Enabled"/> is true.
/// </summary>
internal sealed class HealthMonitorOptions
{
    public const string SectionName = "HealthMonitor";

    /// <summary>Master switch. When false, the monitor is never started.</summary>
    public bool Enabled { get; init; }

    /// <summary>Standard 5-field cron expression (UTC). Default: every minute.</summary>
    [Required]
    public string Cron { get; init; } = "* * * * *";

    /// <summary>How many consecutive not-ready polls in a row before an alert fires.</summary>
    [Range(1, 60)]
    public int ConsecutiveFailureThreshold { get; init; } = 3;
}
