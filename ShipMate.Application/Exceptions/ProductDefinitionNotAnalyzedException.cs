namespace ShipMate.Application.Exceptions;

public class ProductDefinitionNotAnalyzedException : AppException
{
    public override string ErrorCode => "PRODUCT_DEFINITION_NOT_ANALYZED";
    public override int StatusCode => 409;

    public ProductDefinitionNotAnalyzedException()
        : base("Run the AI analysis first — there is nothing to review yet.")
    {
    }
}
