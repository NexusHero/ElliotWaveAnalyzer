using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Logging;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Orchestrates Elliott Wave validation:
/// 1. Validates the annotation list (fast, no I/O)
/// 2. Fetches candles for the annotated period from the appropriate provider
/// 3. Delegates to <see cref="IGeminiWaveAnalyzer"/> for the actual assessment
///
/// Candle fetching provides Gemini with price context beyond just the annotation points,
/// improving the quality of its analysis (trend direction, overall price range).
/// </summary>
public sealed class WaveAnalysisService(
    IEnumerable<IMarketDataProvider> providers,
    IGeminiWaveAnalyzer geminiAnalyzer,
    ILogger<WaveAnalysisService>? logger = null) : IWaveAnalysisService
{
    private readonly IReadOnlyList<IMarketDataProvider> _providers = providers.ToList();

    /// <inheritdoc/>
    public async Task<WaveValidationResult> ValidateAsync(
        string symbol,
        IReadOnlyList<WaveAnnotation> annotations,
        CancellationToken cancellationToken = default)
    {
        ValidateAnnotations(annotations);

        var provider = _providers.FirstOrDefault(p => p.Supports(symbol))
            ?? throw new ArgumentException(
                $"No market data provider supports symbol '{symbol}'.", nameof(symbol));

        // Fetch enough days to cover the annotated period plus some context before it.
        var firstAnnotation = annotations.Min(a => a.Date);
        var lastAnnotation = annotations.Max(a => a.Date);
        var annotatedDays = (int)(lastAnnotation - firstAnnotation).TotalDays;
        var daysToFetch = Math.Max(annotatedDays + 14, 90); // at least 90 days for context

        logger?.LogInformation(
            "Fetching {Days} days of candles for {Symbol} to provide Gemini context",
            daysToFetch, symbol);

        var candles = await provider.GetCandlesAsync(symbol, daysToFetch, cancellationToken);

        return await geminiAnalyzer.ValidateAsync(symbol, candles, annotations, cancellationToken);
    }

    // ─── Input validation (pure, no I/O) ─────────────────────────────────────

    private static void ValidateAnnotations(IReadOnlyList<WaveAnnotation> annotations)
    {
        if (annotations.Count < 2)
            throw new ArgumentException(
                "At least 2 annotations are required for Elliott Wave validation. " +
                $"Received: {annotations.Count}.",
                nameof(annotations));

        // Validate labels
        var invalidLabels = annotations
            .Where(a => !WaveAnnotation.IsValidLabel(a.Label))
            .Select(a => a.Label)
            .ToList();

        if (invalidLabels.Count > 0)
            throw new ArgumentException(
                $"Invalid wave label(s): {string.Join(", ", invalidLabels.Select(l => $"'{l}'"))}. " +
                "Valid labels: 1 2 3 4 5 A B C W X Y",
                nameof(annotations));

        // Validate chronological order
        for (var i = 1; i < annotations.Count; i++)
        {
            if (annotations[i].Date <= annotations[i - 1].Date)
                throw new ArgumentException(
                    $"Annotations must be in chronological order. " +
                    $"Wave '{annotations[i].Label}' ({annotations[i].Date:yyyy-MM-dd}) " +
                    $"is not after wave '{annotations[i - 1].Label}' ({annotations[i - 1].Date:yyyy-MM-dd}).",
                    nameof(annotations));
        }
    }
}
