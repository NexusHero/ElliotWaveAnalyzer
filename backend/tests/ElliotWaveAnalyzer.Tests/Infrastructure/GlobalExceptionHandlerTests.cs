using System.Text.Json;
using ElliotWaveAnalyzer.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="GlobalExceptionHandler"/>: it writes a 500 Problem Details response
/// and reports it as handled, without leaking the exception detail to the client.
/// </summary>
[TestFixture]
public sealed class GlobalExceptionHandlerTests
{
    [Test]
    public async Task TryHandleAsync_WritesProblemDetails500_AndReturnsTrue()
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        using var body = new MemoryStream();
        context.Response.Body = body;

        var handled = await handler.TryHandleAsync(
            context, new InvalidOperationException("secret internal detail"), CancellationToken.None);

        body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(body);
        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
            Assert.That(doc.RootElement.GetProperty("status").GetInt32(), Is.EqualTo(500));
            // The internal exception message must not leak to the client.
            Assert.That(doc.RootElement.GetRawText(), Does.Not.Contain("secret internal detail"));
        });
    }
}
