using System.Security.Claims;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>
/// Endpoint group for authentication: register, login, logout, and the current-user probe.
/// Login issues an opaque session as an HttpOnly cookie; the cookie is the only credential
/// the browser ever holds (never exposed to JavaScript).
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", Register)
            .WithName("Register")
            .WithSummary("Create a new account");

        group.MapPost("/login", Login)
            .WithName("Login")
            .WithSummary("Log in and receive an HttpOnly session cookie")
            .RequireRateLimiting("login");

        group.MapPost("/logout", Logout)
            .WithName("Logout")
            .WithSummary("Revoke the current session and clear the cookie");

        group.MapGet("/me", Me)
            .WithName("Me")
            .WithSummary("Return the currently authenticated user")
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> Register(RegisterRequest request, IAuthService auth, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.Problem(title: "Registration failed", detail: "Email and password are required.", statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await auth.RegisterAsync(request.Email, request.Password, ct);
        return result.Succeeded
            ? Results.Ok()
            : Results.Problem(title: "Registration failed", detail: string.Join(" ", result.Errors), statusCode: StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> Login(LoginRequest request, HttpContext context, IAuthService auth, IOptions<AuthOptions> options, CancellationToken ct)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString();
        var userAgent = context.Request.Headers.UserAgent.ToString();

        var result = await auth.LoginAsync(request.Email, request.Password, ip, userAgent, ct);
        if (!result.Succeeded)
        {
            return Results.Problem(title: "Login failed", detail: result.Error, statusCode: StatusCodes.Status401Unauthorized);
        }

        context.Response.Cookies.Append(options.Value.CookieName, result.Token!, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = result.ExpiresAt,
            Path = "/",
        });

        return Results.Ok(new { email = request.Email });
    }

    private static async Task<IResult> Logout(HttpContext context, IAuthService auth, IOptions<AuthOptions> options, CancellationToken ct)
    {
        var cookieName = options.Value.CookieName;
        if (context.Request.Cookies.TryGetValue(cookieName, out var token))
        {
            await auth.LogoutAsync(token, ct);
        }

        context.Response.Cookies.Delete(cookieName);
        return Results.Ok();
    }

    private static IResult Me(ClaimsPrincipal user) =>
        Results.Ok(new
        {
            id = user.FindFirstValue(ClaimTypes.NameIdentifier),
            email = user.FindFirstValue(ClaimTypes.Email),
        });
}

/// <summary>Request body for <c>POST /api/auth/register</c>.</summary>
public sealed record RegisterRequest(string Email, string Password);

/// <summary>Request body for <c>POST /api/auth/login</c>.</summary>
public sealed record LoginRequest(string Email, string Password);
