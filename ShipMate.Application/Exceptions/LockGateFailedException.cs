using ShipMate.Application.DTOs.ProductDefinitions;

namespace ShipMate.Application.Exceptions;

public class LockGateFailedException : AppException
{
    public override string ErrorCode => "LOCK_GATE_FAILED";
    public override int StatusCode => 409;

    public LockGateDto Gate { get; }

    public override object Details => Gate;

    public LockGateFailedException(LockGateDto gate)
        : base("The MVP can't be locked yet. Resolve what is listed in details.")
    {
        Gate = gate;
    }
}
