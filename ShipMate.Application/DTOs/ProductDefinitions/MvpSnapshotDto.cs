using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.ProductDefinitions;

public class MvpSnapshotDto
{
    public Guid Id { get; set; }
    public int Version { get; set; }
    public string Hash { get; set; } = string.Empty;
    public int DevCountAtLock { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<SnapshotFeatureDto> Features { get; set; } = new();
    public List<SnapshotDependencyDto> Dependencies { get; set; } = new();
}

public class SnapshotFeatureDto
{
    public Guid FeatureId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Scope { get; set; }
    public FeatureRole? Role { get; set; }
    public FeatureOrigin Origin { get; set; }
    public int Complexity { get; set; }
    public SnapshotFeatureStatus Status { get; set; }
}

public class SnapshotDependencyDto
{
    public Guid FeatureId { get; set; }
    public Guid DependsOnFeatureId { get; set; }
}
