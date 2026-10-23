using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Application.Validators.ProductDefinitions;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests.Application;

public class ValidatorTests
{
    private static CommittedFeatureChangeRequest ValidExclude() => new()
    {
        Action = FeatureChangeAction.Exclude,
        Reason = "Customer dropped it",
        CustomerNotifiedConfirmed = true
    };

    [Fact]
    public void ChangeRequest_ValidExclude_Passes()
    {
        Assert.True(new CommittedFeatureChangeRequestValidator().Validate(ValidExclude()).IsValid);
    }

    [Fact]
    public void ChangeRequest_WithoutReason_Fails()
    {
        var request = ValidExclude();
        request.Reason = "";

        Assert.False(new CommittedFeatureChangeRequestValidator().Validate(request).IsValid);
    }

    [Fact]
    public void ChangeRequest_WithoutCustomerConfirmation_Fails()
    {
        var request = ValidExclude();
        request.CustomerNotifiedConfirmed = false;

        Assert.False(new CommittedFeatureChangeRequestValidator().Validate(request).IsValid);
    }

    [Fact]
    public void ChangeRequest_IncludeAction_Fails()
    {
        var request = ValidExclude();
        request.Action = FeatureChangeAction.Include;

        Assert.False(new CommittedFeatureChangeRequestValidator().Validate(request).IsValid);
    }

    [Fact]
    public void ChangeRequest_EditChangingNothing_Fails()
    {
        var request = ValidExclude();
        request.Action = FeatureChangeAction.Edit;

        Assert.False(new CommittedFeatureChangeRequestValidator().Validate(request).IsValid);
    }

    [Fact]
    public void ChangeRequest_EditWithName_Passes()
    {
        var request = ValidExclude();
        request.Action = FeatureChangeAction.Edit;
        request.Name = "New name";

        Assert.True(new CommittedFeatureChangeRequestValidator().Validate(request).IsValid);
    }

    [Fact]
    public void Constraints_AllPartsOptional()
    {
        Assert.True(new UpdateProjectConstraintsRequestValidator().Validate(new UpdateProjectConstraintsRequest()).IsValid);
    }

    [Fact]
    public void Constraints_PartOver5000Characters_Fails()
    {
        var request = new UpdateProjectConstraintsRequest { Team = new string('x', 5001) };

        Assert.False(new UpdateProjectConstraintsRequestValidator().Validate(request).IsValid);
    }

    [Fact]
    public void Constraints_PartAt5000Characters_Passes()
    {
        var request = new UpdateProjectConstraintsRequest { Company = new string('x', 5000) };

        Assert.True(new UpdateProjectConstraintsRequestValidator().Validate(request).IsValid);
    }
}
