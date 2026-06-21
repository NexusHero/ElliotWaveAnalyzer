namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Generates and delivers the daily report for all configured symbols. Triggered by the
/// scheduler, but exposed as a service so it is testable and could also be invoked manually.
/// </summary>
public interface IDailyReportService
{
    /// <summary>
    /// For each configured symbol: fetch analysis, render the chart, and deliver it through
    /// every enabled channel. Failures for one symbol or channel are logged and do not abort
    /// the rest.
    /// </summary>
    Task RunAsync(CancellationToken cancellationToken = default);
}
