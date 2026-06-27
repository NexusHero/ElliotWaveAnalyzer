using System.Security.Claims;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
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

    /// <summary>
    /// Maps the provider-discovery probe and (when configured) the Google OAuth login and
    /// callback. Kept here, next to <see cref="AppendSessionCookie"/>, so Program.cs stays a
    /// thin composition root.
    /// </summary>
    public static IEndpointRouteBuilder MapGoogleAuthEndpoints(
        this IEndpointRouteBuilder app, IConfiguration configuration, bool googleEnabled)
    {
        // Lets the frontend decide whether to render the "Continue with Google" button.
        app.MapGet("/api/auth/providers", () => Results.Ok(new { google = googleEnabled }))
            .AllowAnonymous();

        if (!googleEnabled)
        {
            return app;
        }

        // Where to send the browser once it holds our session cookie. Configurable so the
        // deployed frontend origin can differ from the local Vite dev server.
        var postLoginRedirect = configuration["Authentication:Google:PostLoginRedirectUri"]
            ?? "http://localhost:5173";

        // Step 1: kick off the OAuth dance; Google returns to our callback (not the SPA),
        // so we can exchange the external identity for our own opaque session.
        app.MapGet("/api/auth/google/login", () =>
            Results.Challenge(
                new AuthenticationProperties { RedirectUri = "/api/auth/google/callback" },
                [GoogleDefaults.AuthenticationScheme]))
            .AllowAnonymous();

        // Step 2: read the verified external identity, provision/find the user, issue our
        // session cookie, then redirect into the SPA. On any failure, bounce back with a flag.
        app.MapGet("/api/auth/google/callback",
            async (HttpContext http, IAuthService auth, IOptions<AuthOptions> authOptions, CancellationToken ct) =>
            {
                var external = await http.AuthenticateAsync("ExternalCookie");
                var email = external.Principal?.FindFirstValue(ClaimTypes.Email);
                if (!external.Succeeded || string.IsNullOrWhiteSpace(email))
                {
                    return Results.Redirect($"{postLoginRedirect}?error=google");
                }

                // Only honour emails Google reports as verified (account-takeover guard).
                var emailVerified = string.Equals(
                    external.Principal?.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase);

                var ip = http.Connection.RemoteIpAddress?.ToString();
                var userAgent = http.Request.Headers.UserAgent.ToString();
                var session = await auth.ExternalLoginAsync(email, emailVerified, ip, userAgent, ct);

                // The external cookie is transient — once we have our own session, drop it.
                await http.SignOutAsync("ExternalCookie");

                if (!session.Succeeded)
                {
                    return Results.Redirect($"{postLoginRedirect}?error=google");
                }

                AppendSessionCookie(http, authOptions.Value, session.Token!, session.ExpiresAt);
                return Results.Redirect(postLoginRedirect);
            })
            .AllowAnonymous();

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

        AppendSessionCookie(context, options.Value, result.Token!, result.ExpiresAt);

        return Results.Ok(new { email = request.Email });
    }

    /// <summary>
    /// Writes the opaque session token as the HttpOnly session cookie. Shared by the
    /// password login and the Google OAuth callback so both issue an identical cookie.
    /// </summary>
    internal static void AppendSessionCookie(HttpContext context, AuthOptions options, string token, DateTimeOffset? expiresAt) =>
        context.Response.Cookies.Append(options.CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = expiresAt,
            Path = "/",
        });

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
