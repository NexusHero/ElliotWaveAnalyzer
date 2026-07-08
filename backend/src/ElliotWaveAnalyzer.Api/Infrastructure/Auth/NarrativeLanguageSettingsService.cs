using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// Narrative-language preference store on the shared <see cref="AppUser"/> row (#228) — a single
/// scalar per user, so it lives directly on the user rather than a separate table. Lives in
/// Infrastructure because it touches EF directly — consumers depend on
/// <see cref="INarrativeLanguageSettingsService"/>.
/// </summary>
internal sealed class NarrativeLanguageSettingsService(AppDbContext db) : INarrativeLanguageSettingsService
{
    /// <inheritdoc/>
    public async Task<NarrativeLanguage?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        return user?.NarrativeLanguage;
    }

    /// <inheritdoc/>
    public async Task SetAsync(Guid userId, NarrativeLanguage language, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return;
        }

        user.NarrativeLanguage = language;
        await db.SaveChangesAsync(cancellationToken);
    }
}
