namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Tracks consecutive unhealthy readiness results and decides when the streak counts as
/// "sustained" (#173 AC3) rather than a transient blip — a single failed check should never page
/// anyone; a run of them should, exactly once per episode. Pure, stateful-but-deterministic, so
/// the alerting decision is unit-testable without a real clock or a real background loop.
/// </summary>
public sealed class SustainedFailureDetector(int consecutiveFailureThreshold)
{
    private int _consecutiveFailures;
    private bool _alerted;

    /// <summary>
    /// Records one check's outcome. Returns true exactly once per sustained-failure episode — the
    /// call on which the threshold is first crossed — and stays false on every subsequent still-failing
    /// call until a healthy result resets the streak (so a fixed problem can page again if it recurs).
    /// </summary>
    public bool RecordAndShouldAlert(bool healthy)
    {
        if (healthy)
        {
            _consecutiveFailures = 0;
            _alerted = false;
            return false;
        }

        _consecutiveFailures++;
        if (_consecutiveFailures < consecutiveFailureThreshold || _alerted)
        {
            return false;
        }

        _alerted = true;
        return true;
    }
}
