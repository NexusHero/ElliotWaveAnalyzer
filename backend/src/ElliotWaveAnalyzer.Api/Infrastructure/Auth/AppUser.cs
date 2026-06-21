using Microsoft.AspNetCore.Identity;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// Application user. Extends ASP.NET Core Identity's user with a GUID primary key.
/// Identity owns password hashing, lockout, and normalized-email uniqueness.
/// </summary>
public sealed class AppUser : IdentityUser<Guid>;
