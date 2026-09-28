namespace ShipMate.Application.DTOs.ProductDefinitions;

/// <summary>A dependency link dropped because a re-analysis removed the feature it pointed to (DEFINE step 5b).</summary>
public record RemovedDependencyDto(Guid FeatureId, string FeatureName, string RemovedPrerequisiteName);
