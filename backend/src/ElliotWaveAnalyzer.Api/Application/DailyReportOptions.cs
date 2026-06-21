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
    public string Cron { get; init; } = "0 8 * * *";

    /// <summary>Symbols to include in the report (e.g. BTC, ETH, NASDAQ).</summary>
    public string[] Symbols { get; init; } = ["BTC", "ETH"];

    /// <summary>Days of candle history to render per symbol.</summary>
    public int Days { get; init; } = 90;

    public TelegramOptions Telegram { get; init; } = new();
    public EmailOptions Email { get; init; } = new();
}

/// <summary>Telegram Bot API delivery settings.</summary>
public sealed class TelegramOptions
{
    public bool Enabled { get; init; }
    public string BotToken { get; init; } = string.Empty;
    public string ChatId { get; init; } = string.Empty;
}

/// <summary>SMTP email delivery settings.</summary>
public sealed class EmailOptions
{
    public bool Enabled { get; init; }
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public bool UseSsl { get; init; } = true;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string From { get; init; } = string.Empty;
    public string[] To { get; init; } = [];
}
