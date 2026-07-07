using ElliotWaveAnalyzer.Api.Domain.Account;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// Implements the DSGVO self-service data rights (#168) directly against <see cref="AppDbContext"/>
/// and <see cref="UserManager{TUser}"/>. Deletion relies on the FK cascade configured in
/// <see cref="AppDbContext.OnModelCreating"/> — <see cref="UserManager{TUser}.DeleteAsync"/> alone
/// removes every dependent row at the database level, so there is no per-table delete list here to
/// fall out of sync as new user-owned tables are added.
/// </summary>
internal sealed class AccountRightsService(
    UserManager<AppUser> userManager,
    AppDbContext db,
    TimeProvider timeProvider,
    ILogger<AccountRightsService> logger) : IAccountRightsService
{
    /// <inheritdoc/>
    public async Task<AccountExport> ExportDataAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("Account not found.");

        var snapshots = await db.AnalysisSnapshots
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Include(s => s.Scenarios)
            .Include(s => s.SwitchEvents)
            .ToListAsync(cancellationToken);

        var keys = await db.UserApiKeys
            .AsNoTracking()
            .Where(k => k.UserId == userId)
            .Select(k => new AccountExportApiKey(k.Provider, k.Last4, k.IsDefault, k.CreatedAt))
            .ToListAsync(cancellationToken);

        var depots = await db.SavedDepots
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .Include(d => d.Positions)
            .ToListAsync(cancellationToken);

        var usage = await db.UserLlmUsagePeriods
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new AccountExportLlmUsagePeriod(p.PeriodStart, p.CallCount))
            .ToListAsync(cancellationToken);

        return new AccountExport(
            new AccountExportProfile(user.Id, user.Email ?? string.Empty, user.EmailConfirmed),
            [.. snapshots.Select(ToExportAnalysis)],
            keys,
            [.. depots.Select(ToExportDepot)],
            usage);
    }

    /// <inheritdoc/>
    public async Task<AccountDeletionResult> DeleteAccountAsync(
        Guid userId, string? currentPassword, string? requestedByIp, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return new AccountDeletionResult(false, "Account not found.");
        }

        // Only an account that actually has a password needs to re-confirm one — an OAuth-only
        // account never sets one, and the authenticated session is itself the confirmation there.
        if (user.PasswordHash is not null
            && (string.IsNullOrEmpty(currentPassword) || !await userManager.CheckPasswordAsync(user, currentPassword)))
        {
            return new AccountDeletionResult(false, "Incorrect password.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AccountDeletionResult(false, string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        // Written only once the deletion itself has succeeded — a failed attempt leaves no audit
        // trail behind, matching the "record the deletion" scope of AC4, not "record every attempt".
        db.AccountDeletionAudits.Add(new AccountDeletionAudit
        {
            Id = Guid.NewGuid(),
            DeletedUserId = userId,
            DeletedAt = timeProvider.GetUtcNow(),
            RequestedByIp = requestedByIp,
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Log the non-PII user id, never the email (cs/exposure-of-sensitive-information).
        logger.LogInformation("Account {UserId} deleted", userId);
        return new AccountDeletionResult(true, null);
    }

    private static AccountExportAnalysis ToExportAnalysis(AnalysisSnapshot s) => new(
        s.Id, s.Symbol, s.CreatedAt, s.Structure, s.Bullish,
        s.InvalidationPrice, s.InvalidationAbove, s.TargetLow, s.TargetHigh,
        s.EntryLow, s.EntryHigh, s.Confidence, s.Score,
        s.AlertedOutcome.ToString(), s.EntryZoneAlerted,
        [.. s.Scenarios.OrderBy(r => r.OrderIndex).Select(ToExportScenario)],
        [.. s.SwitchEvents.OrderBy(e => e.At).Select(ToExportSwitchEvent)]);

    private static AccountExportScenario ToExportScenario(AnalysisScenarioRow r) => new(
        r.Role.ToString(), r.OrderIndex, r.Label, r.Structure, r.Bullish,
        r.InvalidationPrice, r.InvalidationAbove, r.EntryLow, r.EntryHigh,
        r.TargetLow, r.TargetHigh, r.Confidence, r.Score, r.Retired);

    private static AccountExportSwitchEvent ToExportSwitchEvent(AnalysisSwitchEventRow e) =>
        new(e.At, e.FromLabel, e.ToLabel, e.Reason);

    private static AccountExportDepot ToExportDepot(SavedDepot d) => new(
        d.Id, d.Source.ToString(), d.ImportedAt, d.ExportedAt, d.Currency,
        d.TotalValue, d.GainAbsolute, d.GainRelativePercent,
        [.. d.Positions.OrderBy(p => p.Ordinal).Select(ToExportPosition)]);

    private static AccountExportDepotPosition ToExportPosition(SavedDepotPosition p) => new(
        p.Ordinal, p.Isin, p.Wkn, p.Name, p.Quantity, p.CostPrice, p.CostValue,
        p.MarketPrice, p.MarketValue, p.GainAbsolute, p.GainRelativePercent, p.Exchange);
}
