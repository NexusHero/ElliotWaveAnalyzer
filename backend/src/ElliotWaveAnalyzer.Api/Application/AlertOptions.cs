using System.ComponentModel.DataAnnotations;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Configuration for scheduled price alerts. Bound from <c>appsettings.json → Alerts</c>. The
/// whole feature is inert unless <see cref="Enabled"/> is true. Delivery reuses the daily
/// report's channels (<c>DailyReport:Telegram</c> / <c>DailyReport:Email</c>).
/// </summary>
public sealed class AlertOptions
{
    public const string SectionName = "Alerts";

    /// <summary>Master switch. When false, the alert scheduler is never started.</summary>
    public bool Enabled { get; init; }

    /// <summary>Standard 5-field cron expression (UTC). Default: top of every hour.</summary>
    [Required]
    public string Cron { get; init; } = "0 * * * *";

    /// <summary>Days of candle history to render on the alert chart.</summary>
    public int ChartDays { get; init; } = 90;
}
