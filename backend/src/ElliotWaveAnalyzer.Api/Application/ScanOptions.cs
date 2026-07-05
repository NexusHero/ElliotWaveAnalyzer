namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Configuration for the setup scanner. Bound from <c>appsettings.json → Scan</c>. The default
/// universe is used when a request doesn't name symbols; the cap bounds how many symbols one scan
/// may analyze (cost/latency guard) — requests beyond it are truncated with the count reported.
/// </summary>
public sealed class ScanOptions
{
    public const string SectionName = "Scan";

    /// <summary>Symbols scanned when a request names none.</summary>
    public IReadOnlyList<string> DefaultSymbols { get; init; } = ["BTC", "ETH"];

    /// <summary>Hard cap on symbols analyzed per scan (no silent overrun).</summary>
    public int MaxSymbols { get; init; } = 50;

    /// <summary>Max symbols analyzed concurrently.</summary>
    public int MaxConcurrency { get; init; } = 8;
}
