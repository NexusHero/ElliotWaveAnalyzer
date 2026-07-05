namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>Request body for <c>POST /api/auth/register</c>.</summary>
public sealed record RegisterRequest(string Email, string Password);
