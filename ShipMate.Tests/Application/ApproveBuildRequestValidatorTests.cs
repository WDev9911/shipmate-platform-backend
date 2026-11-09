using ShipMate.Application.DTOs.Build;
using ShipMate.Application.Validators.Build;

namespace ShipMate.Tests.Application;

public class ApproveBuildRequestValidatorTests
{
    private readonly ApproveBuildRequestValidator _validator = new();

    [Fact]
    public void NoCommit_Passes() =>
        Assert.True(_validator.Validate(new ApproveBuildRequest()).IsValid);

    [Fact]
    public void AFullCommitHash_Passes() =>
        Assert.True(_validator.Validate(new ApproveBuildRequest { CommitSha = new string('a', 40) }).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("abc123")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void AnythingElse_IsRejected(string sha) =>
        Assert.False(_validator.Validate(new ApproveBuildRequest { CommitSha = sha }).IsValid);
}
