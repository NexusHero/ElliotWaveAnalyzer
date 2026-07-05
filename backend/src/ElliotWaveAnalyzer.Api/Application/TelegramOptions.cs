namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>Telegram Bot API delivery settings.</summary>
public sealed class TelegramOptions
{
    public bool Enabled { get; init; }
    public string BotToken { get; init; } = string.Empty;
    public string ChatId { get; init; } = string.Empty;
}
