using Microsoft.EntityFrameworkCore;
using ShipMate.Domain.Entities;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
