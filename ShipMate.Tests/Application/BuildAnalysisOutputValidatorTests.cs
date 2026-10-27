using ShipMate.Application.Ai.Build;

namespace ShipMate.Tests.Application;

public class BuildAnalysisOutputValidatorTests
{
    private static readonly string Other = Guid.NewGuid().ToString();

    private static BuildAnalysisOutput Output(decimal score = 0.9m, params string[] suspected) => new()
    {
        ConfidenceScore = score,
        SuspectedFeatureIds = suspected.ToList()
    };

    private static IReadOnlyList<string> Validate(BuildAnalysisOutput output) =>
        BuildAnalysisOutputValidator.Validate(output, [Other]);

    [Fact]
    public void ACleanResult_Passes() => Assert.Empty(Validate(Output()));

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void ScoreOutsideZeroToOne_IsRejected(double score) =>
        Assert.NotEmpty(Validate(Output((decimal)score)));

    [Fact]
    public void SuspectingAKnownFeature_Passes() =>
        Assert.Empty(Validate(Output(0.3m, Other.ToUpperInvariant())));

    [Fact]
    public void SuspectingAFeatureThatIsNotInTheSnapshot_IsRejected() =>
        Assert.NotEmpty(Validate(Output(0.3m, Guid.NewGuid().ToString())));

    [Fact]
    public void SuspectingTheSameFeatureTwice_IsRejected() =>
        Assert.NotEmpty(Validate(Output(0.3m, Other, Other)));

    [Fact]
    public void AFindingWithoutADescription_IsRejected()
    {
        var output = Output();
        output.Findings.Add(new BuildFinding("a.ts", " "));

        Assert.NotEmpty(Validate(output));
    }

    [Fact]
    public void ASuggestionWithoutAReason_IsRejected()
    {
        var output = Output();
        output.Suggestions.Add(new BuildSuggestion("a.ts", "too heavy", "use a function", ""));

        Assert.NotEmpty(Validate(output));
    }
}
