using ShipMate.Application.DTOs.Build;
using ShipMate.Application.Validators.Build;

namespace ShipMate.Tests.Application;

public class SubmitBuildRequestValidatorTests
{
    private readonly SubmitBuildRequestValidator _validator = new();
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public void Valid_Request_Passes() =>
        Assert.True(_validator.Validate(new SubmitBuildRequest { BranchName = "feature/x", CommitSha = Sha }).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("abc123")]
    [InlineData("0123456789abcdef0123456789abcdef0123456")]
    [InlineData("zzzz456789abcdef0123456789abcdef01234567")]
    public void CommitSha_MustBeAFullHash(string sha) =>
        Assert.False(_validator.Validate(new SubmitBuildRequest { BranchName = "feature/x", CommitSha = sha }).IsValid);

    [Fact]
    public void BranchName_IsRequired() =>
        Assert.False(_validator.Validate(new SubmitBuildRequest { BranchName = "", CommitSha = Sha }).IsValid);
}
