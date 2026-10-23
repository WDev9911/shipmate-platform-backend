using AutoMapper;
using NSubstitute;
using ShipMate.Application.Ai.Lock;
using ShipMate.Application.DTOs.Ai;
using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Application.Validators.ProductDefinitions;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests.Application;

// The AI gateway is faked with fixed JSON, so no real AI call (and no token) is involved.
public class FeatureComplexityTests
{
    private readonly IProductDefinitionRepository _repository = Substitute.For<IProductDefinitionRepository>();
    private readonly IWorkspaceAccessGuard _guard = Substitute.For<IWorkspaceAccessGuard>();
    private readonly IAiGateway _ai = Substitute.For<IAiGateway>();
    private readonly FeatureComplexityService _service;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _workspaceId = Guid.NewGuid();
    private readonly Feature _a = TestData.Feature("a");
    private readonly Feature _b = TestData.Feature("b");
    private readonly Feature _cut = TestData.Feature("cut", status: FeatureStatus.Excluded);
    private readonly ProductDefinition _definition;

    public FeatureComplexityTests()
    {
        _service = new FeatureComplexityService(_repository, _guard, _ai, Substitute.For<IMapper>());
        _definition = TestData.Definition(_a, _b, _cut);
        _definition.Status = ProductDefinitionStatus.ReadyForLock;
        _repository.GetByWorkspaceIdAsync(_workspaceId).Returns(_definition);
        _repository.HasSuccessfulAnalysisRunAsync(_definition.Id).Returns(true);
    }

    private void AiReturns(string json) =>
        _ai.GenerateJsonAsync(Arg.Any<AiJsonRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AiJsonResponse { Json = json, Model = "fake" });

    private static string Scores(params (Guid Id, int Complexity)[] scores) =>
        "{\"scores\":[" + string.Join(",", scores.Select(s =>
            $"{{\"feature_id\":\"{s.Id}\",\"complexity\":{s.Complexity},\"reason\":\"r\"}}")) + "]}";

    // ---- propose ----

    [Fact]
    public async Task Propose_StoresProposalsForIncludedFeaturesOnly()
    {
        AiReturns(Scores((_a.Id, 2), (_b.Id, 5)));

        await _service.ProposeAsync(_userId, _workspaceId);

        Assert.Equal(2, _a.ProposedComplexity);
        Assert.Equal(5, _b.ProposedComplexity);
        Assert.Null(_cut.ProposedComplexity);
        Assert.Null(_a.Complexity);
        await _repository.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Propose_KeepsReadyForLock()
    {
        AiReturns(Scores((_a.Id, 2), (_b.Id, 3)));

        await _service.ProposeAsync(_userId, _workspaceId);

        Assert.Equal(ProductDefinitionStatus.ReadyForLock, _definition.Status);
    }

    [Fact]
    public async Task Propose_AiScoreOutOfRange_IsRejectedAndNothingSaved()
    {
        AiReturns(Scores((_a.Id, 9), (_b.Id, 3)));

        await Assert.ThrowsAsync<AiOutputInvalidException>(() => _service.ProposeAsync(_userId, _workspaceId));

        Assert.Null(_a.ProposedComplexity);
        await _repository.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Propose_AiSkipsAFeature_IsRejected()
    {
        AiReturns(Scores((_a.Id, 2)));

        await Assert.ThrowsAsync<AiOutputInvalidException>(() => _service.ProposeAsync(_userId, _workspaceId));
    }

    [Fact]
    public async Task Propose_MalformedJson_IsRejected()
    {
        AiReturns("not json");

        await Assert.ThrowsAsync<AiOutputInvalidException>(() => _service.ProposeAsync(_userId, _workspaceId));
    }

    [Fact]
    public async Task Propose_NothingIncluded_DoesNotCallTheAi()
    {
        _a.Status = FeatureStatus.Excluded;
        _b.Status = FeatureStatus.Excluded;

        await _service.ProposeAsync(_userId, _workspaceId);

        await _ai.DidNotReceive().GenerateJsonAsync(Arg.Any<AiJsonRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Propose_NeverAnalyzed_IsRejected()
    {
        _repository.HasSuccessfulAnalysisRunAsync(_definition.Id).Returns(false);

        await Assert.ThrowsAsync<ProductDefinitionNotAnalyzedException>(() => _service.ProposeAsync(_userId, _workspaceId));
    }

    [Fact]
    public async Task Propose_WhenLocked_IsRejected()
    {
        _definition.Status = ProductDefinitionStatus.Locked;

        await Assert.ThrowsAsync<ProductDefinitionLockedException>(() => _service.ProposeAsync(_userId, _workspaceId));
    }

    // ---- confirm ----

    [Fact]
    public async Task Set_StoresConfirmedScoreAndKeepsProposal()
    {
        _a.ProposedComplexity = 2;

        await _service.SetAsync(_userId, _workspaceId, new SetComplexityRequest
        {
            Items = [new FeatureComplexityItem { FeatureId = _a.Id, Complexity = 4 }]
        });

        Assert.Equal(4, _a.Complexity);
        Assert.Equal(2, _a.ProposedComplexity);
        Assert.Equal(ProductDefinitionStatus.ReadyForLock, _definition.Status);
    }

    [Fact]
    public async Task Set_ExcludedFeature_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.SetAsync(_userId, _workspaceId, new SetComplexityRequest
        {
            Items = [new FeatureComplexityItem { FeatureId = _cut.Id, Complexity = 3 }]
        }));

        await _repository.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Set_WhenLocked_IsRejected()
    {
        _definition.Status = ProductDefinitionStatus.Locked;

        await Assert.ThrowsAsync<ProductDefinitionLockedException>(() => _service.SetAsync(_userId, _workspaceId, new SetComplexityRequest
        {
            Items = [new FeatureComplexityItem { FeatureId = _a.Id, Complexity = 3 }]
        }));
    }

    // ---- request validation ----

    [Theory]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(0, false)]
    [InlineData(6, false)]
    public void RequestValidator_AcceptsOnlyOneToFive(int complexity, bool valid)
    {
        var request = new SetComplexityRequest
        {
            Items = [new FeatureComplexityItem { FeatureId = Guid.NewGuid(), Complexity = complexity }]
        };

        Assert.Equal(valid, new SetComplexityRequestValidator().Validate(request).IsValid);
    }

    [Fact]
    public void RequestValidator_RejectsEmptyAndDuplicateItems()
    {
        var id = Guid.NewGuid();
        var duplicate = new SetComplexityRequest
        {
            Items = [new() { FeatureId = id, Complexity = 1 }, new() { FeatureId = id, Complexity = 2 }]
        };

        Assert.False(new SetComplexityRequestValidator().Validate(new SetComplexityRequest()).IsValid);
        Assert.False(new SetComplexityRequestValidator().Validate(duplicate).IsValid);
    }

    // ---- AI output validator ----

    [Fact]
    public void OutputValidator_FlagsUnknownDuplicateAndMissingFeatures()
    {
        var output = new ComplexityScoringOutput
        {
            Scores =
            [
                new() { FeatureId = "a", Complexity = 2 },
                new() { FeatureId = "a", Complexity = 3 },
                new() { FeatureId = "zzz", Complexity = 3 }
            ]
        };

        var errors = ComplexityScoringOutputValidator.Validate(output, ["a", "b"]);

        Assert.Equal(3, errors.Count);
    }
}
