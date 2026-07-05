namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Pure input guards for symbol/query strings. Since the symbol allow-list was replaced with
/// resolver-backed lookup, these are the abuse guard: a length cap and a character whitelist that
/// keep control characters and oversized payloads out before anything hits an upstream API.
/// </summary>
public static class SymbolInput
{
    /// <summary>A free-text search query: non-empty, within the cap, no control characters.</summary>
    public static bool IsValidQuery(string? query, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > maxLength)
        {
            return false;
        }

        foreach (var ch in query)
        {
            if (char.IsControl(ch))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// A data-source symbol/ticker: non-empty, within the cap, and only characters that appear in
    /// real tickers — letters, digits and <c>. - ^ = /</c> (covers <c>BRK-B</c>, <c>^IXIC</c>,
    /// <c>SI=F</c>, <c>BTC-USD</c>).
    /// </summary>
    public static bool IsValidSymbol(string? symbol, int maxLength = 32)
    {
        if (string.IsNullOrWhiteSpace(symbol) || symbol.Length > maxLength)
        {
            return false;
        }

        foreach (var ch in symbol)
        {
            if (!char.IsLetterOrDigit(ch) && ch is not ('.' or '-' or '^' or '=' or '/'))
            {
                return false;
            }
        }

        return true;
    }
}
