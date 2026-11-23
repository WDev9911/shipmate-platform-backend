using ShipMate.Domain.Common;
using ShipMate.Domain.Enums;

namespace ShipMate.Domain.Entities;

/// <summary>
/// Immutable audit trail of admin actions — insert-only, never updated or deleted. An action on an account has a
/// target user; a change to the platform settings has none, and names what changed in <see cref="Subject"/>.
/// </summary>
public class AdminActionLog : BaseEntity
{
    public Guid AdminUserId { get; set; }
    public User AdminUser { get; set; } = null!;

    public Guid? TargetUserId { get; set; }
    public User? TargetUser { get; set; }

    public AdminAction Action { get; set; }
    public string? Subject { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}
