using ShipMate.Application.DTOs.ProductDefinitions;

namespace ShipMate.Application.Exceptions;

public class DependencyCreatesCycleException : AppException
{
    public override string ErrorCode => "DEPENDENCY_CREATES_CYCLE";
    public override int StatusCode => 409;

    // The features forming the cycle, in dependency order, starting and ending with the same feature.
    public IReadOnlyList<FeatureReferenceDto> Cycle { get; }

    public override object Details => new { Cycle };

    public DependencyCreatesCycleException(IReadOnlyList<FeatureReferenceDto> cycle)
        : base("This dependency would create a cycle between features.")
    {
        Cycle = cycle;
    }
}
