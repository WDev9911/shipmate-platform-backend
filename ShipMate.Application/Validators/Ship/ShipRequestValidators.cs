using FluentValidation;
using ShipMate.Application.DTOs.Ship;
using ShipMate.Domain.Policies;

namespace ShipMate.Application.Validators.Ship;

public class StartShipAttemptRequestValidator : AbstractValidator<StartShipAttemptRequest>
{
    public StartShipAttemptRequestValidator()
    {
        RuleFor(x => x.SnapshotVersion).GreaterThan(0);

        // Only the shape here. Whether the address is acceptable for production is SHIP_URL_INVALID, with a reason.
        RuleFor(x => x.Url).NotEmpty().MaximumLength(ProductionUrlPolicy.MaxLength);
    }
}
