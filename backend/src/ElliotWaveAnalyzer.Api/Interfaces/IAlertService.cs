namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Re-evaluates saved analyses that are still pending and delivers an alert for each one that
/// has just been invalidated or reached its target. Triggered by the scheduler, but exposed as
/// a service so it is directly testable and could be invoked manually.
/// </summary>
public interface IAlertService
{
    /// <summary>
    /// Runs one alert pass across all still-pending saved analyses. Returns the number of
    /// alerts delivered. Failures for one symbol or channel are logged and do not abort the rest.
    /// </summary>
    Task<int> RunAsync(CancellationToken cancellationToken = default);
}
