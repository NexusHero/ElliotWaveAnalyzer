namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>
/// Request body for <c>POST /api/auth/register</c>. <see cref="AcceptTerms"/> defaults to
/// <see langword="false"/> (fail closed) — a client that omits the field is rejected rather than
/// silently treated as having accepted (#167 AC2).
/// </summary>
public sealed record RegisterRequest(string Email, string Password, bool AcceptTerms = false);
