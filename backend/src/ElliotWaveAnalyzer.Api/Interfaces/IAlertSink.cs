namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Where an operator alert (#173 AC3 — sustained readiness failure) is delivered. OCP: a real
/// paging/chat integration (PagerDuty, Opsgenie, Slack) is a new implementation registered in DI;
/// nothing about the detection logic changes.
/// </summary>
public interface IAlertSink
{
    Task SendAsync(string message, CancellationToken cancellationToken = default);
}
