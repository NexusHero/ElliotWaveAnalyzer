using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Per-user, persisted call quota for LLM-backed features on the operator's shared key (#174). Every
/// operation is scoped to the calling user and the current period; deterministic (no-LLM) features
/// never consult this at all.
/// </summary>
public interface IUserLlmQuotaService
{
    /// <summary>
    /// Atomically checks and, if allowed, consumes one call from the user's current-period quota.
    /// Returns false without consuming anything when the user is already at their limit.
    /// </summary>
    Task<bool> TryConsumeAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Returns the user's current-period quota standing (for display, not gating).</summary>
    Task<UserQuotaStatus> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default);
}
