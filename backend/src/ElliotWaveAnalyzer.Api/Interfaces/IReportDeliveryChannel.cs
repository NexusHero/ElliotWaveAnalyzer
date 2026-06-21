using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// A delivery target for a rendered report (Telegram, Email, …). New channels are added
/// by implementing this interface and registering them — the report service iterates all
/// enabled channels and never changes (OCP).
/// </summary>
public interface IReportDeliveryChannel
{
    /// <summary>Channel name for logging (e.g. "Telegram", "Email").</summary>
    string Name { get; }

    /// <summary>Whether the channel is configured and should be used.</summary>
    bool IsEnabled { get; }

    /// <summary>Delivers the report artifact through this channel.</summary>
    Task SendAsync(ReportArtifact report, CancellationToken cancellationToken = default);
}
