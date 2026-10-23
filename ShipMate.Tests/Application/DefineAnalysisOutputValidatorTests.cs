using ShipMate.Application.Ai.Define;
using ShipMate.Domain.Constants;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests.Application;

// DEFINE M3: rules the AI output must pass before anything is saved.
public class DefineAnalysisOutputValidatorTests
{
    private const string Committed = "committed-1";
    private const string Kept = "kept-1";

    private static DefineFeatureOutput Item(
        string id,
        FeatureOrigin origin = FeatureOrigin.FromAi,
        FeatureRole role = FeatureRole.Primary,
        params string[] dependsOn) => new()
    {
        Id = id,
        Name = $"Feature {id}",
        Description = "d",
        Scope = "s",
        Role = role,
        Origin = origin,
        AiAssessment = new DefineAiAssessmentOutput { Verdict = AiVerdict.Required, Reason = "r" },
        DependsOn = dependsOn.Select(d => new DefineDependencyOutput { FeatureId = d, Reason = "r" }).ToList()
    };

    private static DefineAnalysisOutput Output(params DefineFeatureOutput[] features) => new()
    {
        Persona = new DefinePersona { PrimaryPersona = "Bệnh nhân", Reason = "r" },
        ProblemSolution = new DefineProblemSolutionOutput { Problem = "p", Solution = "s" },
        Features = features.ToList()
    };

    private static IReadOnlyList<string> Validate(DefineAnalysisOutput output) =>
        DefineAnalysisOutputValidator.Validate(output, [Committed], [Committed, Kept]);

    [Fact]
    public void ValidOutput_HasNoErrors()
    {
        var output = Output(
            Item(Committed, FeatureOrigin.FromCommittedList, FeatureRole.Primary, "n1"),
            Item("n1", role: FeatureRole.Supporting));

        Assert.Empty(Validate(output));
    }

    [Fact]
    public void EmptyPersona_IsRejected()
    {
        var output = Output(Item(Committed, FeatureOrigin.FromCommittedList));
        output.Persona.PrimaryPersona = " ";

        Assert.Contains(Validate(output), e => e.Contains("persona"));
    }

    [Fact]
    public void SameIdTwice_IsRejected()
    {
        var output = Output(Item(Committed, FeatureOrigin.FromCommittedList), Item("n1"), Item("n1"));

        Assert.Contains(Validate(output), e => e.Contains("used more than once"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(FeatureConstraints.NameMaxLength + 1)]
    public void EmptyOrTooLongName_IsRejected(int length)
    {
        var feature = Item("n1");
        feature.Name = new string('a', length);

        Assert.Contains(Validate(Output(Item(Committed, FeatureOrigin.FromCommittedList), feature)), e => e.Contains("name"));
    }

    [Fact]
    public void AiFeatureClaimingTheCommittedOrigin_IsRejected()
    {
        var output = Output(Item(Committed, FeatureOrigin.FromCommittedList), Item("n1", FeatureOrigin.FromCommittedList));

        Assert.Contains(Validate(output), e => e.Contains("'n1' uses origin from_committed_list incorrectly"));
    }

    [Fact]
    public void CommittedFeatureReturnedWithAnotherOrigin_IsRejected()
    {
        var output = Output(Item(Committed, FeatureOrigin.FromAi));

        Assert.Contains(Validate(output), e => e.Contains($"'{Committed}' uses origin from_committed_list incorrectly"));
    }

    [Fact]
    public void MissingCommittedFeature_IsRejected()
    {
        Assert.Contains(Validate(Output(Item("n1"))), e => e.Contains($"Committed feature '{Committed}' is missing"));
    }

    [Fact]
    public void DependencyOnAnUnknownFeature_IsRejected()
    {
        var output = Output(Item(Committed, FeatureOrigin.FromCommittedList, FeatureRole.Primary, "ghost"));

        Assert.Contains(Validate(output), e => e.Contains("unknown feature 'ghost'"));
    }

    [Fact]
    public void DependencyOnAKeptFeatureLeftOutOfTheOutput_IsAllowed()
    {
        var output = Output(Item(Committed, FeatureOrigin.FromCommittedList, FeatureRole.Primary, Kept));

        Assert.Empty(Validate(output));
    }

    [Fact]
    public void DependencyOnItself_IsRejected()
    {
        var output = Output(Item(Committed, FeatureOrigin.FromCommittedList), Item("n1", FeatureOrigin.FromAi, FeatureRole.Primary, "n1"));

        Assert.Contains(Validate(output), e => e.Contains("depends on itself"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("n2")]
    public void DuplicateFlagNotPointingToACommittedFeature_IsRejected(string? duplicateOf)
    {
        var feature = Item("n1");
        feature.PossibleDuplicate = true;
        feature.DuplicateOf = duplicateOf;

        Assert.Contains(Validate(Output(Item(Committed, FeatureOrigin.FromCommittedList), feature, Item("n2"))),
            e => e.Contains("duplicate_of is not a committed feature id"));
    }

    [Fact]
    public void DuplicateFlagPointingToACommittedFeature_IsAllowed()
    {
        var feature = Item("n1");
        feature.PossibleDuplicate = true;
        feature.DuplicateOf = Committed;

        Assert.Empty(Validate(Output(Item(Committed, FeatureOrigin.FromCommittedList), feature)));
    }

    [Fact]
    public void NewSupportingFeatureNoPrimaryNeeds_IsRejected()
    {
        var output = Output(Item(Committed, FeatureOrigin.FromCommittedList), Item("n1", role: FeatureRole.Supporting));

        Assert.Contains(Validate(output), e => e.Contains("Supporting feature 'n1' is not required by any primary feature"));
    }

    [Fact]
    public void KeptSupportingFeatureNoPrimaryNeeds_IsAllowed()
    {
        var output = Output(Item(Committed, FeatureOrigin.FromCommittedList), Item(Kept, FeatureOrigin.FromCustomerMentioned, FeatureRole.Supporting));

        Assert.Empty(Validate(output));
    }
}
