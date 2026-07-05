using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Resolves a free-text query — a ticker, a company name, or an <b>ISIN</b> (e.g. from an imported
/// depot position) — to the instruments a data source can serve. Best match first. Empty when
/// nothing matches (never throws for "not found"). Kept narrow (ISP): resolution only.
/// </summary>
public interface ISymbolResolver
{
    /// <summary>Returns matching instruments, best match first; empty when none match.</summary>
    Task<IReadOnlyList<ResolvedSymbol>> SearchAsync(string query, CancellationToken cancellationToken = default);
}
