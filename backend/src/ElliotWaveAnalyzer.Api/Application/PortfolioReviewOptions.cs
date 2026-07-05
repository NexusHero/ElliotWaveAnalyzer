using System.ComponentModel.DataAnnotations;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Configuration for the optional scheduled portfolio-review refresh. Bound from
/// <c>appsettings.json → PortfolioReview</c>. The on-demand endpoint always works; the scheduled
/// warm-through of every user's depot is inert unless <see cref="Enabled"/> is true.
/// </summary>
public sealed class PortfolioReviewOptions
{
    public const string SectionName = "PortfolioReview";

    /// <summary>Master switch. When false, the refresh scheduler is never started.</summary>
    public bool Enabled { get; init; }

    /// <summary>Standard 5-field cron expression (UTC). Default: daily at 06:00.</summary>
    [Required]
    public string Cron { get; init; } = "0 6 * * *";
}
