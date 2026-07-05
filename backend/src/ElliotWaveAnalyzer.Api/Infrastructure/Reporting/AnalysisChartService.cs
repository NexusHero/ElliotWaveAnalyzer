using ElliotWaveAnalyzer.Api.Application.Charting;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Reporting;

/// <summary>
/// Assembles a saved analysis and its live candles into an <see cref="AnnotatedChartInput"/>, composes
/// the backend-agnostic scene with <see cref="AnnotatedChartComposer"/>, and rasterizes it via
/// <see cref="IAnnotatedChartRenderer"/>. Ownership is delegated to <see cref="ITrackRecordService.GetAsync"/>
/// (returns null for a missing or other-user analysis, which the endpoint maps to 404). Lives in
/// Infrastructure because it fetches market data and drives the SkiaSharp backend.
/// </summary>
internal sealed class AnalysisChartService(
    ITrackRecordService trackRecord,
    IEnumerable<IMarketDataProvider> marketDataProviders,
    IAnnotatedChartRenderer renderer,
    TimeProvider timeProvider) : IAnalysisChartService
{
    private readonly IReadOnlyList<IMarketDataProvider> _marketDataProviders = [.. marketDataProviders];

    /// <summary>Roughly a year of daily context around the analysis.</summary>
    private const int ChartWindowDays = 365;

    /// <inheritdoc/>
    public async Task<byte[]?> RenderChartAsync(
        Guid userId, Guid analysisId, CancellationToken cancellationToken = default)
    {
        var analysis = await trackRecord.GetAsync(userId, analysisId, cancellationToken);
        if (analysis is null)
        {
            return null;
        }

        var candles = await GetCandlesAsync(analysis.Symbol, cancellationToken);
        var input = BuildInput(analysis, candles, timeProvider.GetUtcNow().UtcDateTime);
        var scene = AnnotatedChartComposer.Compose(input);
        return renderer.Render(scene);
    }

    private async Task<IReadOnlyList<MarketCandle>> GetCandlesAsync(
        string symbol, CancellationToken cancellationToken)
    {
        var provider = _marketDataProviders.FirstOrDefault(p => p.Supports(symbol));
        if (provider is null)
        {
            return [];
        }

        try
        {
            return await provider.GetCandlesAsync(symbol, ChartWindowDays, cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            // A data-source hiccup must not fail the export — render the annotations on an empty pane.
            return [];
        }
    }

    private static AnnotatedChartInput BuildInput(
        TrackedAnalysis analysis, IReadOnlyList<MarketCandle> candles, DateTime renderDate)
    {
        var primary = analysis.Scenarios.FirstOrDefault(s => s.Role == ScenarioRole.Primary);

        return new AnnotatedChartInput(analysis.Symbol, "1D", renderDate, FibScale.Linear, candles)
        {
            Invalidation = analysis.InvalidationPrice is { } inv
                ? new PriceLevel(
                    inv,
                    analysis.InvalidationAbove ? LevelSide.Above : LevelSide.Below,
                    "Invalidation",
                    analysis.Structure)
                : null,
            EntryZone = ToZone(primary?.EntryLow, primary?.EntryHigh, "Entry", "Pullback zone"),
            TargetZones = ToZone(analysis.TargetLow, analysis.TargetHigh, "Target", "Projected target") is { } t
                ? [t]
                : [],
            Scenarios = [.. analysis.Scenarios
                .Where(s => !s.Retired && (s.TargetLow ?? s.TargetHigh) is not null)
                .Select(s => new ChartScenarioArrow(
                    s.Label, s.Bullish, s.Role == ScenarioRole.Primary, Midpoint(s.TargetLow, s.TargetHigh)))],
        };
    }

    private static PriceZone? ToZone(decimal? low, decimal? high, string label, string basis)
    {
        if (low is null && high is null)
        {
            return null;
        }

        var lo = low ?? high!.Value;
        var hi = high ?? low!.Value;
        return new PriceZone(Math.Min(lo, hi), Math.Max(lo, hi), label, basis);
    }

    private static decimal Midpoint(decimal? low, decimal? high)
    {
        if (low is not null && high is not null)
        {
            return (low.Value + high.Value) / 2m;
        }

        return low ?? high ?? 0m;
    }
}
