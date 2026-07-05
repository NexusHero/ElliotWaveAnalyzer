using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance for vision import (<c>POST /api/wave-analysis/verify-image</c>): an uploaded
/// chart is verified over the full stack (auth, multipart, the faked vision LLM, real candles, the
/// snap/guard/rule pipeline). The canned extraction is built from the fake provider's actual candle
/// extremes so the claimed pivots snap and produce rule verdicts. Oversized/wrong-type → 400;
/// unauthenticated → 401.
/// </summary>
[TestFixture]
public sealed class ImageVerificationAcceptanceTests
{
    private AcceptanceWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        TestDocker.SkipIfUnavailable();
        _factory = new AcceptanceWebApplicationFactory();
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();
        await _factory.AuthenticateAsync(_client);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    private static byte[] SamplePng()
    {
        using var bitmap = new SKBitmap(16, 16);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static MultipartFormDataContent Upload(byte[] image, string contentType, string? symbol)
    {
        var file = new ByteArrayContent(image);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var content = new MultipartFormDataContent { { file, "file", "chart.png" } };
        if (symbol is not null)
        {
            content.Add(new StringContent(symbol), "symbol");
        }

        return content;
    }

    /// <summary>Builds a canned extraction from six real candle extremes so the claim snaps exactly.</summary>
    private async Task PrimeVisionFromRealCandlesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var technical = scope.ServiceProvider.GetRequiredService<ITechnicalAnalysisService>();
        var analysis = await technical.GetAnalysisAsync("BTC", 730, CandleInterval.OneDay);
        var candles = analysis.Candles;

        var indices = new[] { 0, 100, 200, 300, 400, 500 };
        var pivots = indices.Select((idx, label) =>
        {
            var c = candles[idx];
            return $$"""{ "approxDate": "{{c.OpenTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}}", "approxPrice": {{c.High.ToString(CultureInfo.InvariantCulture)}}, "label": "{{label}}" }""";
        });

        _factory.Chat.ResponseJson =
            $$"""{ "symbol": "BTC", "timeframe": "1D", "pivots": [ {{string.Join(", ", pivots)}} ], "levels": [], "zones": [] }""";
    }

    [Test]
    public async Task VerifyImage_UploadedChart_ReturnsAVerificationReport()
    {
        await PrimeVisionFromRealCandlesAsync();

        var response = await _client.PostAsync("/api/wave-analysis/verify-image", Upload(SamplePng(), "image/png", "BTC"));
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(root.GetProperty("status").GetString(), Is.EqualTo("Verified"));
            Assert.That(root.GetProperty("snapped").GetArrayLength(), Is.EqualTo(6));
            Assert.That(root.GetProperty("claimedRules").GetProperty("rules").GetArrayLength(), Is.GreaterThan(0));
            Assert.That(root.TryGetProperty("comparison", out _), Is.True);
        });
    }

    [Test]
    public async Task VerifyImage_WrongContentType_Returns400()
    {
        var bytes = Encoding.UTF8.GetBytes("not an image");
        var response = await _client.PostAsync("/api/wave-analysis/verify-image", Upload(bytes, "text/plain", "BTC"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task VerifyImage_OversizedFile_Returns400()
    {
        var big = new byte[9 * 1024 * 1024];
        var response = await _client.PostAsync("/api/wave-analysis/verify-image", Upload(big, "image/png", "BTC"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task VerifyImage_Unauthenticated_Returns401()
    {
        using var anon = _factory.CreateClient();

        var response = await anon.PostAsync("/api/wave-analysis/verify-image", Upload(SamplePng(), "image/png", "BTC"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
