namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>
/// Body of <c>POST /api/risk</c>: a trade idea's geometry plus the account-risk to size against. The
/// geometry (<see cref="Invalidation"/>, <see cref="Targets"/>, <see cref="Bullish"/>) comes straight
/// from a count's projection; <see cref="Entry"/> and the account-risk are the user's own inputs (the
/// frontend defaults <see cref="Entry"/> to current price). Account-risk may be given as a percentage
/// of equity or as an absolute amount — see <see cref="ResolveRiskCapital"/>.
/// </summary>
/// <param name="Entry">Entry price (positive).</param>
/// <param name="Invalidation">The count's hard invalidation line — used as the stop.</param>
/// <param name="Targets">Target prices (reported ascending with R:R each); may be empty.</param>
/// <param name="Bullish">True for a long (stop below entry), false for a short (stop above entry).</param>
/// <param name="AccountEquity">Account size, used with <see cref="RiskPercent"/> when no
/// <see cref="RiskAmount"/> is given.</param>
/// <param name="RiskPercent">Percent of equity to risk (e.g. 1 = 1%). Ignored when
/// <see cref="RiskAmount"/> is supplied.</param>
/// <param name="RiskAmount">Absolute capital to risk; takes precedence over the percentage form.</param>
public sealed record RiskRequest(
    decimal Entry,
    decimal Invalidation,
    IReadOnlyList<decimal>? Targets,
    bool Bullish,
    decimal? AccountEquity = null,
    decimal? RiskPercent = null,
    decimal? RiskAmount = null)
{
    /// <summary>
    /// The absolute capital to risk: the explicit <see cref="RiskAmount"/> when given, else
    /// <see cref="AccountEquity"/> × <see cref="RiskPercent"/>%. Non-positive or absent inputs
    /// resolve to 0, which the calculator treats as "size omitted" rather than an error.
    /// </summary>
    public decimal ResolveRiskCapital()
        => RiskAmount ?? (AccountEquity ?? 0m) * (RiskPercent ?? 0m) / 100m;
}
