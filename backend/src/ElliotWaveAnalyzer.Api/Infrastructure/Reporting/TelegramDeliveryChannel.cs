using System.Net.Http.Headers;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Reporting;

/// <summary>
/// Delivers a report as a photo message via the Telegram Bot API (<c>sendPhoto</c>).
/// </summary>
internal sealed class TelegramDeliveryChannel(
    HttpClient httpClient,
    IOptions<DailyReportOptions> options,
    ILogger<TelegramDeliveryChannel> logger) : IReportDeliveryChannel
{
    private TelegramOptions Options => options.Value.Telegram;

    /// <inheritdoc/>
    public string Name => "Telegram";

    /// <inheritdoc/>
    public bool IsEnabled =>
        Options.Enabled
        && !string.IsNullOrWhiteSpace(Options.BotToken)
        && !string.IsNullOrWhiteSpace(Options.ChatId);

    /// <inheritdoc/>
    public async Task SendAsync(ReportArtifact report, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent(Options.ChatId), "chat_id" },
            { new StringContent(report.Caption), "caption" },
        };

        var photo = new ByteArrayContent(report.PngImage);
        photo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(photo, "photo", $"{report.Symbol}.png");

        logger.LogDebug("Sending {Symbol} report to Telegram chat {ChatId}", report.Symbol, Options.ChatId);

        var response = await httpClient.PostAsync($"bot{Options.BotToken}/sendPhoto", content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
