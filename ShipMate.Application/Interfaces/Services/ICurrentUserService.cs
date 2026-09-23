namespace ShipMate.Application.Interfaces.Services;

/// <summary>
/// Reads the identity of the currently authenticated request (from the validated JWT claims).
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
}
