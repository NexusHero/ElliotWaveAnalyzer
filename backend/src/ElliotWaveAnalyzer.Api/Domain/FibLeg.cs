namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A reference leg the confluence calculator draws Fibonacci relationships from. Callers supply
/// legs from several degrees (a wave-1 at this degree, the same wave at the parent degree, …); the
/// <see cref="DegreeWeight"/> makes higher-degree levels count for more when zones are scored.
/// </summary>
/// <param name="Start">Leg start price.</param>
/// <param name="End">Leg end price.</param>
/// <param name="Label">Analyst label for the leg, e.g. "(1)→(2)".</param>
/// <param name="DegreeWeight">Relative significance of the leg's degree (higher = more).</param>
public sealed record FibLeg(decimal Start, decimal End, string Label, decimal DegreeWeight);
