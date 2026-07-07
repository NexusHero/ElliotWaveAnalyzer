namespace ElliotWaveAnalyzer.Tests.LoadTests;

/// <summary>Aggregated outcome of a <see cref="LoadTestHarness.RunAsync"/> run.</summary>
internal sealed record LoadTestResult(
    int TotalRequests, int ErrorCount, double ErrorRatePercent, double P50Ms, double P95Ms, double P99Ms, double MaxMs)
{
    public override string ToString() =>
        $"requests={TotalRequests} errors={ErrorCount} ({ErrorRatePercent:F2}%) " +
        $"p50={P50Ms:F0}ms p95={P95Ms:F0}ms p99={P99Ms:F0}ms max={MaxMs:F0}ms";
}
