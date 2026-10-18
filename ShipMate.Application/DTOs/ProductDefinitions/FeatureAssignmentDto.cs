using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.ProductDefinitions;

/// <summary>One locked feature with who may take it and who already did.</summary>
public class FeatureAssignmentOverviewDto
{
    public Guid FeatureId { get; set; }
    public string Name { get; set; } = string.Empty;
    public SnapshotFeatureStatus Status { get; set; }
    public int Complexity { get; set; }
    public FeatureAssigneeDto? Assignee { get; set; }
    public List<QualifiedMemberDto> QualifiedMembers { get; set; } = new();
}

public record FeatureAssigneeDto(Guid UserId, string DisplayName, string BranchName);

public record QualifiedMemberDto(Guid UserId, string DisplayName, int SkillLevel);

public class FeatureAssignmentDto
{
    public Guid FeatureId { get; set; }
    public Guid AssignedUserId { get; set; }
    public string BranchName { get; set; } = string.Empty;
}
