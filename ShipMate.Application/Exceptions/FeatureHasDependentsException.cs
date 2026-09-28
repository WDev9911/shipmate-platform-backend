using ShipMate.Application.DTOs.ProductDefinitions;

namespace ShipMate.Application.Exceptions;

public class FeatureHasDependentsException : AppException
{
    public override string ErrorCode => "FEATURE_HAS_DEPENDENTS";
    public override int StatusCode => 409;

    public IReadOnlyList<FeatureReferenceDto> DependentFeatures { get; }

    public override object Details => new { DependentFeatures };

    public FeatureHasDependentsException(IReadOnlyList<FeatureReferenceDto> dependentFeatures)
        : base("Other included features depend on this one. Choose to remove those dependency links or to exclude the dependent features as well.")
    {
        DependentFeatures = dependentFeatures;
    }
}
