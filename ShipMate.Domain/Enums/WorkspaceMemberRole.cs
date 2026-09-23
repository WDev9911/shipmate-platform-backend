namespace ShipMate.Domain.Enums;

/// <summary>
/// Scoped to a single workspace — unrelated to the platform-wide UserRole used for Admin RBAC.
/// A user can be Manager in one workspace and a regular Developer member in another.
/// </summary>
public enum WorkspaceMemberRole
{
    Developer,
    Manager
}
