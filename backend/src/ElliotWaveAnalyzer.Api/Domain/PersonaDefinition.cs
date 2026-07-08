namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// One fixed "analyst persona" the panel (#184) queries: a stable key (used for weighting and
/// track-record tagging), a display name, and the system-prompt guidance that gives the same
/// underlying model a distinct reading angle. Personas only re-order and explain the engine's
/// own deterministic candidates (ADR-009) — the guidance text never asks for geometry.
/// </summary>
public sealed record PersonaDefinition(string Key, string DisplayName, string Guidance);
