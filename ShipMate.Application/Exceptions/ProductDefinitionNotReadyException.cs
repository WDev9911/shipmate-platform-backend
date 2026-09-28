using ShipMate.Application.DTOs.ProductDefinitions;

namespace ShipMate.Application.Exceptions;

public class ProductDefinitionNotReadyException : AppException
{
    public override string ErrorCode => "PRODUCT_DEFINITION_NOT_READY";
    public override int StatusCode => 409;

    public ProductDefinitionReadinessDto Readiness { get; }

    public override object Details => Readiness;

    public ProductDefinitionNotReadyException(ProductDefinitionReadinessDto readiness)
        : base("The product definition isn't ready for LOCK yet. Resolve the pending features, open flags and dependency cycle listed in details.")
    {
        Readiness = readiness;
    }
}
