using System.ComponentModel.DataAnnotations;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Configuration for the scheduled daily report. Bound from <c>appsettings.json → DailyReport</c>.
/// The whole feature is inert unless <see cref="Enabled"/> is true, so it costs nothing
/// when not configured.
/// </summary>
public sealed class DailyReportOptions
{
    public const string SectionName = "DailyReport";

    /// <summary>Master switch. When false, the scheduler is never started.</summary>
    public bool Enabled { get; init; }

    /// <summary>Standard 5-field cron expression (UTC). Default: 08:00 every day.</summary>
    [Required]
    public string Cron { get; init; } = "0 8 * * *";

    /// <summary>Symbols to include in the report (e.g. BTC, ETH, NASDAQ).</summary>
    public string[] Symbols { get; init; } = ["BTC", "ETH"];

    /// <summary>Days of candle history to render per symbol.</summary>
    public int Days { get; init; } = 90;

    public TelegramOptions Telegram { get; init; } = new();
    public EmailOptions Email { get; init; } = new();
}
