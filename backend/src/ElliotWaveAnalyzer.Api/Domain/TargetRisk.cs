namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The reward side of a single target for a risk assessment: how far the target is from entry
/// (in the trade's favour) and that reward expressed as a multiple of the risk taken (R:R).
/// </summary>
/// <param name="Price">The target price.</param>
/// <param name="RewardAbs">Signed reward in price terms (target − entry for a long; entry − target
/// for a short). Negative when the target sits on the losing side of entry.</param>
/// <param name="RewardToRisk">Reward as a multiple of the stop distance (R:R). 3.0 means the target
/// is three times as far from entry as the stop.</param>
public sealed record TargetRisk(decimal Price, decimal RewardAbs, decimal RewardToRisk);
