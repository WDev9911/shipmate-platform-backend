using Microsoft.EntityFrameworkCore;
using Npgsql;
using ShipMate.Application.Exceptions;

namespace ShipMate.Infrastructure.Persistence;

internal static class UniqueViolation
{
    private const string SqlState = "23505";
    private const string SnapshotVersionIndex = "IX_mvp_snapshots_workspace_id_version";
    public const string AuditSequenceIndex = "IX_audit_trail_workspace_id_sequence";

    public static bool Matches(DbUpdateException ex, string? constraintName = null) =>
        ex.InnerException is PostgresException { SqlState: SqlState } postgres
        && (constraintName is null || postgres.ConstraintName == constraintName);

    /// <summary>
    /// Saves, turning a race on the snapshot version into a 409 the client can act on instead of a 500.
    /// </summary>
    public static async Task<bool> SaveGuardingSnapshotVersionAsync(AppDbContext context)
    {
        try
        {
            return await context.SaveChangesAsync() > 0;
        }
        catch (DbUpdateException ex) when (Matches(ex, SnapshotVersionIndex))
        {
            throw new SnapshotVersionConflictException();
        }
    }
}
