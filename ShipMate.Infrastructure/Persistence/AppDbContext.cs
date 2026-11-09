using Microsoft.EntityFrameworkCore;
using Npgsql;
using ShipMate.Application.Exceptions;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Policies;

namespace ShipMate.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<GitHubConnection> GitHubConnections => Set<GitHubConnection>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<AdminActionLog> AdminActionLogs => Set<AdminActionLog>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();
    public DbSet<ProductDefinition> ProductDefinitions => Set<ProductDefinition>();
    public DbSet<Feature> Features => Set<Feature>();
    public DbSet<FeatureDependency> FeatureDependencies => Set<FeatureDependency>();
    public DbSet<FeatureChangeLog> FeatureChangeLogs => Set<FeatureChangeLog>();
    public DbSet<AiAnalysisRun> AiAnalysisRuns => Set<AiAnalysisRun>();
    public DbSet<MvpSnapshot> MvpSnapshots => Set<MvpSnapshot>();
    public DbSet<SnapshotFeature> SnapshotFeatures => Set<SnapshotFeature>();
    public DbSet<SnapshotDependency> SnapshotDependencies => Set<SnapshotDependency>();
    public DbSet<FeatureAssignment> FeatureAssignments => Set<FeatureAssignment>();
    public DbSet<FeatureChallenge> FeatureChallenges => Set<FeatureChallenge>();
    public DbSet<BuildSubmission> BuildSubmissions => Set<BuildSubmission>();
    public DbSet<DriftAlert> DriftAlerts => Set<DriftAlert>();
    public DbSet<AuditTrailEntry> AuditTrail => Set<AuditTrailEntry>();

    // How many times a save is retried when it lost the race to extend a workspace's audit chain.
    private const int MaxAuditChainAttempts = 10;

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await base.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (attempt < MaxAuditChainAttempts && LostTheRaceForTheAuditChain(ex))
            {
                // Another writer extended the chain first: read its last record again and link on top of it.
                await RelinkPendingAuditEntriesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex) when (ex.Entries.Any(e => e.Entity is Workspace))
            {
                throw new ThresholdChangeConflictException();
            }
        }
    }

    private bool LostTheRaceForTheAuditChain(Exception ex)
    {
        // PostgreSQL calls a deadlock transient, so EF wraps it once more than other database errors.
        var update = ex as DbUpdateException ?? ex.InnerException as DbUpdateException;
        if (update is null)
        {
            return false;
        }

        // Writers inserting neighbouring sequences at once can also deadlock on the same unique index. Nothing was written, so it is retried alike.
        var deadlock = update.InnerException is PostgresException { SqlState: "40P01" }
            && ChangeTracker.Entries<AuditTrailEntry>().Any(e => e.State == EntityState.Added);

        return deadlock || UniqueViolation.Matches(update, UniqueViolation.AuditSequenceIndex);
    }

    private async Task RelinkPendingAuditEntriesAsync(CancellationToken cancellationToken)
    {
        var pending = ChangeTracker.Entries<AuditTrailEntry>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .OrderBy(e => e.Sequence)
            .ToList();

        foreach (var workspace in pending.GroupBy(e => e.WorkspaceId))
        {
            var last = await AuditTrail.AsNoTracking()
                .Where(e => e.WorkspaceId == workspace.Key)
                .OrderByDescending(e => e.Sequence)
                .FirstOrDefaultAsync(cancellationToken);

            foreach (var entry in workspace)
            {
                AuditChain.Link(entry, last);
                last = entry;
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
