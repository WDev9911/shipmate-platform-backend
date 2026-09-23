using ShipMate.Domain.Common;
using ShipMate.Domain.Enums;

namespace ShipMate.Domain.Entities;

/// <summary>
/// Immutable audit trail of admin actions taken on a user account — insert-only, never updated or deleted.
/// </summary>
public class AdminActionLog : BaseEntity
{
    public Guid AdminUserId { get; set; }
    public User AdminUser { get; set; } = null!;

    public Guid TargetUserId { get; set; }
    public User TargetUser { get; set; } = null!;

    public AdminAction Action { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}
