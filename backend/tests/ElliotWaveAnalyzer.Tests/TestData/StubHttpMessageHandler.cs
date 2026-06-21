using System.Net;
using System.Text;

namespace ElliotWaveAnalyzer.Tests.TestData;

/// <summary>
/// Minimal <see cref="HttpMessageHandler"/> that returns a canned response, for testing
/// typed HttpClient-based providers without any network call.
/// </summary>
public sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(response);
    }

    /// <summary>Builds an <see cref="HttpClient"/> that always returns <paramref name="json"/> as 200 OK.</summary>
    public static HttpClient JsonClient(string json, string baseUrl)
    {
        var handler = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
        return new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
    }
}
