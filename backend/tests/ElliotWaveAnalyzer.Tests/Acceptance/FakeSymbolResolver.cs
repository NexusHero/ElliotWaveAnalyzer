using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>Deterministic <see cref="ISymbolResolver"/> so the symbol-search endpoint can be
/// acceptance-tested without hitting Yahoo. Any non-blank query resolves to a canned instrument;
/// the literal "zzz" resolves to nothing (the "no matches" path).</summary>
public sealed class FakeSymbolResolver : ISymbolResolver
{
    public Task<IReadOnlyList<ResolvedSymbol>> SearchAsync(
        string query, CancellationToken cancellationToken = default)
    {
        var trimmed = query.Trim();
        IReadOnlyList<ResolvedSymbol> results =
            trimmed.Length == 0 || trimmed.Equals("zzz", StringComparison.OrdinalIgnoreCase)
                ? []
                : [new ResolvedSymbol("AAPL", "Apple Inc.", "EQUITY", "NASDAQ")];
        return Task.FromResult(results);
    }
}
