using System.Net;
using System.Net.Mail;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Reporting;

/// <summary>
/// Delivers a report via SMTP email with the chart attached as a PNG.
/// </summary>
internal sealed class EmailDeliveryChannel(
    IOptions<DailyReportOptions> options,
    ILogger<EmailDeliveryChannel> logger) : IReportDeliveryChannel
{
    private EmailOptions Options => options.Value.Email;

    /// <inheritdoc/>
    public string Name => "Email";

    /// <inheritdoc/>
    public bool IsEnabled =>
        Options.Enabled
        && !string.IsNullOrWhiteSpace(Options.Host)
        && !string.IsNullOrWhiteSpace(Options.From)
        && Options.To.Length > 0;

    /// <inheritdoc/>
    public async Task SendAsync(ReportArtifact report, CancellationToken cancellationToken = default)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(Options.From),
            Subject = $"Daily {report.Symbol} report",
            Body = report.Caption,
        };

        foreach (var recipient in Options.To)
        {
            message.To.Add(recipient);
        }

        using var stream = new MemoryStream(report.PngImage);
        message.Attachments.Add(new Attachment(stream, $"{report.Symbol}.png", "image/png"));

        using var client = new SmtpClient(Options.Host, Options.Port)
        {
            EnableSsl = Options.UseSsl,
            Credentials = new NetworkCredential(Options.Username, Options.Password),
        };

        logger.LogDebug("Emailing {Symbol} report to {Recipients}", report.Symbol, string.Join(", ", Options.To));

        await client.SendMailAsync(message, cancellationToken);
    }
}
