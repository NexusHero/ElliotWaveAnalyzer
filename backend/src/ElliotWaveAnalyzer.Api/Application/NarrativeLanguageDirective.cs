using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// The single source of truth for the "write in this language" instruction appended to a narrative
/// system prompt (#228). Pure and tiny on purpose: every narrator/prompt caller appends the same
/// wording the same way, so there is exactly one place to change the phrasing.
/// </summary>
public static class NarrativeLanguageDirective
{
    /// <summary>
    /// The directive to append to a system prompt, or null for <see cref="NarrativeLanguage.English"/> —
    /// every prompt in this codebase is already written in English, so there is nothing to add.
    /// </summary>
    public static string? For(NarrativeLanguage language) => language switch
    {
        NarrativeLanguage.German =>
            "Write every free-text narrative field (summaries, rationales, outlooks, reasons) in "
            + "German (Deutsch). Keep JSON keys, structure labels and numeric formatting unchanged.",
        _ => null,
    };
}
