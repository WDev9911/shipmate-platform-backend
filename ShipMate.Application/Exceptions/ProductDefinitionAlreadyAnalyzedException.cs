namespace ShipMate.Application.Exceptions;

public class ProductDefinitionAlreadyAnalyzedException : AppException
{
    public override string ErrorCode => "PRODUCT_DEFINITION_ALREADY_ANALYZED";
    public override int StatusCode => 409;

    public ProductDefinitionAlreadyAnalyzedException()
        : base("This product definition has already been analyzed. Re-running the AI analysis is not available yet.")
    {
    }
}
