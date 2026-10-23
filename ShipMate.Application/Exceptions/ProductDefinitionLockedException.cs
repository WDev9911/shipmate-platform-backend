namespace ShipMate.Application.Exceptions;

public class ProductDefinitionLockedException : AppException
{
    public override string ErrorCode => "PRODUCT_DEFINITION_LOCKED";
    public override int StatusCode => 409;

    public ProductDefinitionLockedException()
        : base("The product definition is locked. Scope changes now go through a Feature Challenge.")
    {
    }
}
