using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure.Gemini;
using ElliotWaveAnalyzer.Tests.TestData;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Tests for <see cref="GeminiPromptBuilder"/>.
///
/// The prompt builder is pure (no I/O, no external dependencies), so these tests
/// need NO mocks. They verify that the prompt Gemini receives contains all the
/// information it needs to make a correct assessment.
///
/// WHY test the prompt?
/// A wrong prompt = wrong Gemini output = wrong user feedback.
/// The prompt is logic, not glue code — it deserves tests.
/// </summary>
[TestFixture]
public sealed class GeminiPromptBuilderTests
{
    private static readonly IReadOnlyList<MarketCandle> Candles =
        MarketDataFixtures.CreateCandles(60);

    private static readonly IReadOnlyList<WaveAnnotation> FiveWaveAnnotations =
    [
        new(new DateTime(2024, 1,  5, 0, 0, 0, DateTimeKind.Utc), 38_000m, "1"),
        new(new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc), 35_000m, "2"),
        new(new DateTime(2024, 2,  1, 0, 0, 0, DateTimeKind.Utc), 52_000m, "3"),
        new(new DateTime(2024, 2, 10, 0, 0, 0, DateTimeKind.Utc), 44_000m, "4"),
        new(new DateTime(2024, 2, 25, 0, 0, 0, DateTimeKind.Utc), 58_000m, "5"),
    ];
    private static readonly string[] sourceArray = new[] { "\"1\"", "\"2\"", "\"3\"", "\"4\"", "\"5\"" };

    // ─── Content checks ───────────────────────────────────────────────────────

    [Test]
    public void Build_ContainsSymbol()
    {
        var prompt = GeminiPromptBuilder.Build("BTC", Candles, FiveWaveAnnotations);

        Assert.That(prompt, Does.Contain("BTC"));
    }

    [Test]
    public void Build_ContainsAllWaveLabels()
    {
        var prompt = GeminiPromptBuilder.Build("BTC", Candles, FiveWaveAnnotations);

        foreach (var annotation in FiveWaveAnnotations)
        {
            Assert.That(prompt, Does.Contain(annotation.Label),
                $"Prompt must mention wave label '{annotation.Label}'");
        }
    }

    [Test]
    public void Build_ContainsAnnotationPrices()
    {
        var prompt = GeminiPromptBuilder.Build("BTC", Candles, FiveWaveAnnotations);

        foreach (var annotation in FiveWaveAnnotations)
        {
            Assert.That(prompt, Does.Contain(annotation.Price.ToString("F2")),
                $"Prompt must include price {annotation.Price} for wave {annotation.Label}");
        }
    }

    [Test]
    public void Build_ContainsAnnotationDates()
    {
        var prompt = GeminiPromptBuilder.Build("BTC", Candles, FiveWaveAnnotations);

        foreach (var annotation in FiveWaveAnnotations)
        {
            Assert.That(prompt, Does.Contain(annotation.Date.ToString("yyyy-MM-dd")),
                $"Prompt must include date {annotation.Date:yyyy-MM-dd} for wave {annotation.Label}");
        }
    }

    [Test]
    public void Build_RequestsJsonOutput()
    {
        var prompt = GeminiPromptBuilder.Build("BTC", Candles, FiveWaveAnnotations);

        // Gemini must know to return JSON, not prose
        Assert.That(prompt, Does.Contain("JSON").IgnoreCase);
    }

    [Test]
    public void Build_IncludesElliotWaveRules()
    {
        var prompt = GeminiPromptBuilder.Build("BTC", Candles, FiveWaveAnnotations);

        // The three cardinal Elliott Wave rules must be referenced so Gemini
        // applies them explicitly rather than relying on implicit knowledge.
        Assert.That(prompt, Does.Contain("Wave 2").IgnoreCase,
            "Prompt must reference Wave 2 rule (no retrace beyond Wave 1 start)");
        Assert.That(prompt, Does.Contain("Wave 3").IgnoreCase,
            "Prompt must reference Wave 3 rule (never the shortest)");
        Assert.That(prompt, Does.Contain("Wave 4").IgnoreCase,
            "Prompt must reference Wave 4 rule (no overlap with Wave 1)");
    }

    [Test]
    public void Build_IncludesCandlePriceRange()
    {
        var prompt = GeminiPromptBuilder.Build("ETH", Candles, FiveWaveAnnotations);

        // The candle context (at minimum the price range) gives Gemini
        // the broader market context surrounding the annotated waves.
        var minPrice = Candles.Min(c => c.Low);
        var maxPrice = Candles.Max(c => c.High);

        Assert.That(prompt, Does.Contain(minPrice.ToString("F2"))
            .Or.Contain(((int)minPrice).ToString()),
            "Prompt must include the candle low for context");
        Assert.That(prompt, Does.Contain(maxPrice.ToString("F2"))
            .Or.Contain(((int)maxPrice).ToString()),
            "Prompt must include the candle high for context");
    }

    // ─── Structure / format checks ────────────────────────────────────────────

    [Test]
    public void Build_AnnotationsAreInChronologicalOrder()
    {
        // Even if caller passes unsorted annotations, prompt must be chronological.
        var reversed = FiveWaveAnnotations.Reverse().ToList();
        var prompt = GeminiPromptBuilder.Build("BTC", Candles, reversed);

        // Find positions of wave labels in prompt — must appear in 1→5 order
        var positions = sourceArray.Select(label => prompt.IndexOf(label, StringComparison.Ordinal))
            .ToList();

        Assert.That(positions, Is.Ordered.Ascending,
            "Annotations must appear in chronological order in the prompt");
    }

    [Test]
    public void Build_IncludesWavePriceDeltas()
    {
        // Gemini needs the price movement between waves to check relative sizes.
        // The prompt should include Δ% between consecutive annotations.
        var prompt = GeminiPromptBuilder.Build("BTC", Candles, FiveWaveAnnotations);

        Assert.That(prompt, Does.Contain("%"),
            "Prompt should include percentage price changes between waves");
    }

    [Test]
    public void Build_NeverEmpty()
    {
        var minimal = new List<WaveAnnotation>
        {
            new(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), 40_000m, "1"),
            new(new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), 38_000m, "2"),
        };

        var prompt = GeminiPromptBuilder.Build("BTC", Candles, minimal);

        Assert.That(prompt, Is.Not.Null.And.Not.Empty);
        Assert.That(prompt.Length, Is.GreaterThan(200),
            "A meaningful prompt for Gemini should be at least 200 characters");
    }
}
