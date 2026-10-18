namespace ShipMate.Application.DTOs.Workspaces;

// LOCK collects the skill profile just in time: a member declares it when joining the workspace.
public class AcceptInvitationRequest
{
    public int SkillLevel { get; set; }
}
