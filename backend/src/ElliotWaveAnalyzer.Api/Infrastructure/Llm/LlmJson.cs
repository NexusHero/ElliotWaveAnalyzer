namespace ElliotWaveAnalyzer.Api.Infrastructure.Llm;

/// <summary>
/// Helpers for coaxing a clean JSON object out of an LLM completion. Models occasionally
/// ignore "respond with JSON only" and wrap the payload in markdown fences (```json … ```),
/// add a stray backtick, or prepend a sentence. Rather than special-casing each shape, we
/// extract the substring from the first '{' to the last '}', which survives all of them.
/// </summary>
internal static class LlmJson
{
    /// <summary>
    /// Returns the outermost JSON object found in <paramref name="text"/> (first '{' through
    /// last '}'), trimmed. Falls back to the trimmed input when no braces are present, so the
    /// caller still gets a clear "not valid JSON" error with the raw text.
    /// </summary>
    public static string ExtractObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start
            ? text[start..(end + 1)]
            : text.Trim();
    }
}
