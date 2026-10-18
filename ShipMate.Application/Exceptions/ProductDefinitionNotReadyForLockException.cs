namespace ShipMate.Application.Exceptions;

public class ProductDefinitionNotReadyForLockException : AppException
{
    public override string ErrorCode => "PRODUCT_DEFINITION_NOT_READY_FOR_LOCK";
    public override int StatusCode => 409;

    public ProductDefinitionNotReadyForLockException()
        : base("Mark the product definition READY_FOR_LOCK before locking.")
    {
    }
}
