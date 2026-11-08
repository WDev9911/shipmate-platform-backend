namespace ShipMate.Domain.Enums;

// Account-level role. Manager is not here: it exists only per workspace (WorkspaceMemberRole), given to whoever creates it.
// The numbers stay as they were, since the API also accepts them: 1 (the old Manager) must not turn into Administrator.
public enum UserRole
{
    Developer = 0,
    Administrator = 2
}
