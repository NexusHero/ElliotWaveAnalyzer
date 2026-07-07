using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ElliotWaveAnalyzer.Api.Infrastructure;

/// <summary>
/// Catches any exception that escapes an endpoint and returns an RFC 9457 Problem Details
/// response, logging the detail server-side so nothing internal leaks to the client.
/// </summary>
internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // A canceled request (client navigated away / aborted the fetch) is not a server fault —
        // don't log it as an error or dress it up as a 500. 499 is the de-facto
        // "client closed request" status; the client is gone and never sees it anyway.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // The path is client-controlled; strip line breaks before logging so a crafted request
            // can't forge fake log entries (CodeQL cs/log-forging).
            var safePath = httpContext.Request.Path.Value?.ReplaceLineEndings(" ") ?? string.Empty;
            logger.LogDebug("Request was canceled by the client: {Path}", safePath);
            httpContext.Response.StatusCode = 499;
            return true;
        }

        logger.LogError(exception, "Unhandled exception");

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
        };

        httpContext.Response.StatusCode = problem.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
