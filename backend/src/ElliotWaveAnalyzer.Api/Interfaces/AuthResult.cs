namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>Outcome of a registration attempt.</summary>
public sealed record AuthResult(bool Succeeded, IReadOnlyList<string> Errors);
