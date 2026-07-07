using System.Diagnostics;

namespace ElliotWaveAnalyzer.Tests.LoadTests;

/// <summary>
/// A minimal, dependency-free concurrent load driver (#199 AC5 — the tool choice, documented: a
/// hand-rolled harness over k6/NBomber, since it needs nothing beyond what's already referenced —
/// no separate binary, no cloud-reporting account, consistent with this codebase's established
/// preference for pure-managed, no-extra-moving-parts tooling, e.g. CsCheck over a heavier
/// property-testing framework). Drives <paramref name="request"/> from <paramref name="concurrency"/>
/// worker loops for <paramref name="duration"/>, recording per-call latency and outcome.
/// </summary>
internal static class LoadTestHarness
{
    internal static async Task<LoadTestResult> RunAsync(
        Func<CancellationToken, Task<int>> request, int concurrency, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(duration);

        var samples = new System.Collections.Concurrent.ConcurrentBag<Sample>();

        var workers = Enumerable.Range(0, concurrency).Select(_ => Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                var sw = Stopwatch.StartNew();
                int statusCode;
                var success = true;
                try
                {
                    statusCode = await request(cts.Token);
                    success = statusCode is >= 200 and < 500;
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    statusCode = -1;
                    success = false;
                }

                sw.Stop();
                samples.Add(new Sample(sw.Elapsed.TotalMilliseconds, success, statusCode));
            }
        }));

        await Task.WhenAll(workers);

        return Summarize([.. samples]);
    }

    private static LoadTestResult Summarize(IReadOnlyList<Sample> samples)
    {
        if (samples.Count == 0)
        {
            return new LoadTestResult(0, 0, 0, 0, 0, 0, 0);
        }

        var latencies = samples.Select(s => s.LatencyMs).Order().ToList();
        var errorCount = samples.Count(s => !s.Success);

        return new LoadTestResult(
            TotalRequests: samples.Count,
            ErrorCount: errorCount,
            ErrorRatePercent: 100.0 * errorCount / samples.Count,
            P50Ms: Percentile(latencies, 0.50),
            P95Ms: Percentile(latencies, 0.95),
            P99Ms: Percentile(latencies, 0.99),
            MaxMs: latencies[^1]);
    }

    private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        var index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
        return sortedValues[Math.Clamp(index, 0, sortedValues.Count - 1)];
    }

    private sealed record Sample(double LatencyMs, bool Success, int StatusCode);
}
