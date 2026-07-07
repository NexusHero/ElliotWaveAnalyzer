using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// EF Core context for authentication: the ASP.NET Core Identity tables, the server-side
/// <see cref="UserSession"/> store, and the Data Protection key ring (<see cref="IDataProtectionKeyContext"/>,
/// #171) — so encrypted per-user API keys stay decryptable across restarts and across multiple
/// instances sharing this database, instead of the framework's default local-only key ring.
/// </summary>
internal sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options), IDataProtectionKeyContext
{
    public DbSet<UserSession> Sessions => Set<UserSession>();
    public DbSet<AnalysisSnapshot> AnalysisSnapshots => Set<AnalysisSnapshot>();
    public DbSet<UserApiKey> UserApiKeys => Set<UserApiKey>();
    public DbSet<SavedDepot> SavedDepots => Set<SavedDepot>();
    public DbSet<BacktestRun> BacktestRuns => Set<BacktestRun>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<UserLlmUsagePeriod> UserLlmUsagePeriods => Set<UserLlmUsagePeriod>();
    public DbSet<AccountDeletionAudit> AccountDeletionAudits => Set<AccountDeletionAudit>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<UserSession>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(s => s.TokenHash).IsUnique();
            entity.HasIndex(s => s.UserId);
            // Account deletion (#168, AC3) relies on this FK cascading at the database level —
            // without it, UserManager.DeleteAsync would remove only the AspNetUsers row and leave
            // this table's rows orphaned (a plain uuid column with no FK, discovered while
            // implementing #168).
            entity.HasOne<AppUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AnalysisSnapshot>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Symbol).HasMaxLength(32).IsRequired();
            entity.Property(s => s.Structure).HasMaxLength(64).IsRequired();
            entity.Property(s => s.Confidence).HasMaxLength(16).IsRequired();
            // List the newest first per user — the history's natural order.
            entity.HasIndex(s => new { s.UserId, s.CreatedAt });
            entity.HasMany(s => s.Scenarios)
                .WithOne()
                .HasForeignKey(r => r.AnalysisSnapshotId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(s => s.SwitchEvents)
                .WithOne()
                .HasForeignKey(e => e.AnalysisSnapshotId)
                .OnDelete(DeleteBehavior.Cascade);
            // #168 AC3 — see the UserSession config above for why this is needed explicitly.
            entity.HasOne<AppUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AnalysisScenarioRow>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Label).HasMaxLength(32).IsRequired();
            entity.Property(r => r.Structure).HasMaxLength(64).IsRequired();
            entity.Property(r => r.Confidence).HasMaxLength(16).IsRequired();
            entity.HasIndex(r => r.AnalysisSnapshotId);
        });

        builder.Entity<AnalysisSwitchEventRow>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FromLabel).HasMaxLength(32).IsRequired();
            entity.Property(e => e.ToLabel).HasMaxLength(32).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(128).IsRequired();
            entity.HasIndex(e => e.AnalysisSnapshotId);
        });

        builder.Entity<UserApiKey>(entity =>
        {
            entity.HasKey(k => k.Id);
            entity.Property(k => k.Provider).HasMaxLength(16).IsRequired();
            entity.Property(k => k.Last4).HasMaxLength(4).IsRequired();
            entity.Property(k => k.CipherText).IsRequired();
            // One key per (user, provider).
            entity.HasIndex(k => new { k.UserId, k.Provider }).IsUnique();
            // #168 AC3 — see the UserSession config above for why this is needed explicitly.
            entity.HasOne<AppUser>().WithMany().HasForeignKey(k => k.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SavedDepot>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Currency).HasMaxLength(8).IsRequired();
            // Every import accumulates as a snapshot (#115) — list the newest first per user,
            // mirroring AnalysisSnapshot's history index (ADR-010).
            entity.HasIndex(d => new { d.UserId, d.ImportedAt });
            entity.HasMany(d => d.Positions)
                .WithOne()
                .HasForeignKey(p => p.SavedDepotId)
                .OnDelete(DeleteBehavior.Cascade);
            // #168 AC3 — see the UserSession config above for why this is needed explicitly.
            entity.HasOne<AppUser>().WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SavedDepotPosition>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Isin).HasMaxLength(12).IsRequired();
            entity.Property(p => p.Wkn).HasMaxLength(12);
            entity.Property(p => p.Name).HasMaxLength(128).IsRequired();
            entity.Property(p => p.Exchange).HasMaxLength(32);
            entity.HasIndex(p => p.SavedDepotId);
        });

        builder.Entity<BacktestRun>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.DatasetHash).HasMaxLength(64).IsRequired();
            entity.Property(r => r.EngineVersion).HasMaxLength(16).IsRequired();
            entity.Property(r => r.Symbol).HasMaxLength(32).IsRequired();
            entity.Property(r => r.Config).HasMaxLength(256).IsRequired();
            // One run per dataset — the hash is the idempotency key for a re-run.
            entity.HasIndex(r => r.DatasetHash).IsUnique();
            entity.HasMany(r => r.Buckets)
                .WithOne()
                .HasForeignKey(b => b.BacktestRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BacktestBucketRow>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Dimension).HasMaxLength(16).IsRequired();
            entity.Property(b => b.Key).HasMaxLength(32).IsRequired();
            entity.HasIndex(b => b.BacktestRunId);
        });

        builder.Entity<UserLlmUsagePeriod>(entity =>
        {
            entity.HasKey(p => p.Id);
            // One row per (user, period) — the unit TryConsumeAsync's atomic UPDATE targets.
            entity.HasIndex(p => new { p.UserId, p.PeriodStart }).IsUnique();
            // #168 AC3 — see the UserSession config above for why this is needed explicitly.
            entity.HasOne<AppUser>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AccountDeletionAudit>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.DeletedUserId);
            // Deliberately no FK to AspNetUsers — the audit row (#168 AC4) must survive the very
            // deletion it records, so it cannot cascade with the user it references.
        });
    }
}
