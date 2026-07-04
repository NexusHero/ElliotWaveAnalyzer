using System.Net;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure.Reporting;
using ElliotWaveAnalyzer.Tests.TestData;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="TelegramDeliveryChannel"/>: the enabled predicate and that a send
/// posts a photo to the Telegram Bot API. The HTTP boundary is stubbed — no network.
/// </summary>
[TestFixture]
public sealed class TelegramDeliveryChannelTests
{
    private static DailyReportOptions Options(bool enabled, string token = "bot-token", string chat = "123") =>
        new()
        {
            Telegram = new TelegramOptions { Enabled = enabled, BotToken = token, ChatId = chat },
        };

    private static TelegramDeliveryChannel Build(DailyReportOptions options, StubHttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.telegram.org/") };
        return new TelegramDeliveryChannel(client, Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<TelegramDeliveryChannel>.Instance);
    }

    private static StubHttpMessageHandler OkHandler() =>
        new(new HttpResponseMessage(HttpStatusCode.OK));

    [Test]
    public void IsEnabled_RequiresFlagTokenAndChatId()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Build(Options(enabled: true), OkHandler()).IsEnabled, Is.True);
            Assert.That(Build(Options(enabled: false), OkHandler()).IsEnabled, Is.False);
            Assert.That(Build(Options(enabled: true, token: ""), OkHandler()).IsEnabled, Is.False);
            Assert.That(Build(Options(enabled: true, chat: ""), OkHandler()).IsEnabled, Is.False);
        });
    }

    [Test]
    public void Name_IsTelegram() => Assert.That(Build(Options(true), OkHandler()).Name, Is.EqualTo("Telegram"));

    [Test]
    public async Task SendAsync_PostsPhotoToTheBotApi()
    {
        var handler = OkHandler();
        var sut = Build(Options(true, token: "abc123"), handler);
        var artifact = new ReportArtifact("BTC", [1, 2, 3], "caption");

        await sut.SendAsync(artifact);

        Assert.Multiple(() =>
        {
            Assert.That(handler.LastRequest, Is.Not.Null);
            Assert.That(handler.LastRequest!.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(handler.LastRequest.RequestUri!.ToString(), Does.Contain("botabc123/sendPhoto"));
        });
    }

    [Test]
    public void SendAsync_OnHttpError_Throws()
    {
        var handler = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.BadRequest));
        var sut = Build(Options(true), handler);

        Assert.ThrowsAsync<HttpRequestException>(
            () => sut.SendAsync(new ReportArtifact("BTC", [1], "x")));
    }
}
