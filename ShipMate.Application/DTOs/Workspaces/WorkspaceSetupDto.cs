namespace ShipMate.Application.DTOs.Workspaces;

public class WorkspaceSetupDto
{
    // True when nothing below blocks BUILD from running on this workspace.
    public bool Ready { get; set; }

    public bool RepoLinked { get; set; }
    public bool AppInstalled { get; set; }
    public bool MergeGateEnabled { get; set; }

    // Not a blocker: a personal repo only gets a weaker lock, so the UI warns instead of failing the check.
    public bool RepoIsOrganization { get; set; }

    public bool GovernanceSuspended { get; set; }

    public List<Guid> MembersWithoutSkill { get; set; } = [];
    public List<Guid> MembersWithoutGitHub { get; set; } = [];
}
