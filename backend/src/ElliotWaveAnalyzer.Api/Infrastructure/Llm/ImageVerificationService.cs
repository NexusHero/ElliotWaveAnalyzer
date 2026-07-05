using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Llm;

/// <summary>
/// Orchestrates image verification: extract the claimed count (vision), resolve the instrument and
/// timeframe (caller-supplied values win over what the model read), fetch the real candles, and hand
/// both to the pure <see cref="ChartVerificationAssembler"/>. The image is parsed in-request and never
/// persisted (privacy default). Only the extraction touches an LLM; everything downstream is deterministic.
/// </summary>
internal sealed class ImageVerificationService(
    IChartVisionExtractor extractor,
    ITechnicalAnalysisService technicalAnalysis) : IImageVerificationService
{
    /// <summary>Daily-equivalent history to pull so the claimed pivots' dates are covered.</summary>
    private const int LookbackDays = 730;

    /// <inheritdoc/>
    public async Task<ImageVerificationReport> VerifyAsync(
        byte[] image, string contentType, string? symbol, string? timeframe, CancellationToken cancellationToken = default)
    {
        var extraction = await extractor.ExtractAsync(image, contentType, cancellationToken);

        var resolvedSymbol = FirstNonBlank(symbol, extraction.Symbol)
            ?? throw new ArgumentException(
                "The instrument could not be determined from the image; provide a symbol.", nameof(symbol));
        var interval = ParseTimeframe(FirstNonBlank(timeframe, extraction.Timeframe));

        var analysis = await technicalAnalysis.GetAnalysisAsync(
            resolvedSymbol, LookbackDays, interval, cancellationToken);

        return ChartVerificationAssembler.Assemble(extraction, analysis.Candles);
    }

    private static string? FirstNonBlank(string? a, string? b)
        => !string.IsNullOrWhiteSpace(a) ? a.Trim() : !string.IsNullOrWhiteSpace(b) ? b.Trim() : null;

    private static CandleInterval ParseTimeframe(string? timeframe) => timeframe?.Trim().ToUpperInvariant() switch
    {
        "1W" or "W" or "WEEKLY" => CandleInterval.OneWeek,
        "4H" => CandleInterval.FourHours,
        "1H" or "H" => CandleInterval.OneHour,
        _ => CandleInterval.OneDay,
    };
}
