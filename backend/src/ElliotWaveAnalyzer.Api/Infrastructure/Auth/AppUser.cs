using ElliotWaveAnalyzer.Api.Domain;
using Microsoft.AspNetCore.Identity;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// Application user. Extends ASP.NET Core Identity's user with a GUID primary key.
/// Identity owns password hashing, lockout, and normalized-email uniqueness.
/// </summary>
internal sealed class AppUser : IdentityUser<Guid>
{
    /// <summary>
    /// The language LLM narratives are written in for this user (#228). Null means "never
    /// explicitly chosen" — narrators treat that as English, and the frontend uses it as the
    /// signal to suggest (and persist) a browser-locale default on first load (AC4).
    /// </summary>
    public NarrativeLanguage? NarrativeLanguage { get; set; }
}
