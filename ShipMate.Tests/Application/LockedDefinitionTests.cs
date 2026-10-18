using AutoMapper;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Application.DTOs.Workspaces;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests.Application;

// DEFINE step 9: after the first Lock, DEFINE (including the vision prompt) is frozen.
public class LockedDefinitionTests
{
    private readonly IProductDefinitionRepository _repository = Substitute.For<IProductDefinitionRepository>();
    private readonly IWorkspaceAccessGuard _guard = Substitute.For<IWorkspaceAccessGuard>();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _workspaceId = Guid.NewGuid();
    private readonly Feature _feature = TestData.Feature("a", FeatureOrigin.FromCommittedList);
    private readonly Workspace _workspace = new() { VisionPrompt = "original vision" };
    private readonly ProductDefinition _definition;

    public LockedDefinitionTests()
    {
        _definition = TestData.Definition(_feature);
        _definition.Status = ProductDefinitionStatus.Locked;
        _repository.GetByWorkspaceIdAsync(_workspaceId).Returns(_definition);
        _repository.HasSuccessfulAnalysisRunAsync(_definition.Id).Returns(true);
        _guard.GetWorkspaceAsManagerAsync(_userId, _workspaceId).Returns(_workspace);
    }

    private ProductDefinitionService DefinitionService() =>
        new(_repository, _guard, Substitute.For<IMapper>());

    private FeatureReviewService ReviewService() =>
        new(_repository, _guard, Substitute.For<IMapper>());

    private async Task AssertLockedAndNothingSaved(Func<Task> action)
    {
        await Assert.ThrowsAsync<ProductDefinitionLockedException>(action);

        await _repository.DidNotReceive().SaveChangesAsync();
        Assert.Equal(ProductDefinitionStatus.Locked, _definition.Status);
    }

    [Fact]
    public Task MarkReady_IsRejected() =>
        AssertLockedAndNothingSaved(() => DefinitionService().MarkReadyForLockAsync(_userId, _workspaceId));

    [Fact]
    public Task UpdateConstraints_IsRejected() =>
        AssertLockedAndNothingSaved(() => DefinitionService().UpdateConstraintsAsync(
            _userId, _workspaceId, new UpdateProjectConstraintsRequest { Team = "x" }));

    [Fact]
    public Task AddCommittedFeature_IsRejected() =>
        AssertLockedAndNothingSaved(() => DefinitionService().AddCommittedFeatureAsync(
            _userId, _workspaceId, new CreateCommittedFeatureRequest { Name = "n", Description = "d" }));

    [Fact]
    public Task UpdateCommittedFeature_IsRejected() =>
        AssertLockedAndNothingSaved(() => DefinitionService().UpdateCommittedFeatureAsync(
            _userId, _workspaceId, _feature.Id, new UpdateCommittedFeatureRequest { Name = "x" }));

    [Fact]
    public Task DeleteCommittedFeature_IsRejected() =>
        AssertLockedAndNothingSaved(() => DefinitionService().DeleteCommittedFeatureAsync(
            _userId, _workspaceId, _feature.Id));

    [Fact]
    public Task ChangeFeatureStatus_IsRejected() =>
        AssertLockedAndNothingSaved(() => ReviewService().ChangeStatusAsync(
            _userId, _workspaceId, _feature.Id, new UpdateFeatureStatusRequest { Status = FeatureStatus.Included }));

    [Fact]
    public Task EditFeature_IsRejected() =>
        AssertLockedAndNothingSaved(() => ReviewService().EditAsync(
            _userId, _workspaceId, _feature.Id, new UpdateFeatureRequest { Name = "x" }));

    [Fact]
    public Task RemoveDependency_IsRejected() =>
        AssertLockedAndNothingSaved(() => ReviewService().RemoveDependencyAsync(
            _userId, _workspaceId, _feature.Id, Guid.NewGuid()));

    [Fact]
    public async Task Analyze_IsRejectedBeforeAnyRunIsRecorded()
    {
        var service = new ProductDefinitionAnalysisService(
            _repository, _guard, Substitute.For<IAiGateway>(), Substitute.For<IMapper>(),
            Substitute.For<ILogger<ProductDefinitionAnalysisService>>());

        await AssertLockedAndNothingSaved(() => service.AnalyzeAsync(_userId, _workspaceId, null));

        await _repository.DidNotReceive().AddAnalysisRunAsync(Arg.Any<AiAnalysisRun>());
    }

    private WorkspaceService WorkspaceService() => new(
        Substitute.For<IWorkspaceRepository>(),
        Substitute.For<IWorkspaceMemberRepository>(),
        _guard,
        _repository,
        Substitute.For<IUserRepository>(),
        Substitute.For<IGitHubConnectionRepository>(),
        Substitute.For<IGitHubOAuthService>(),
        Substitute.For<IEncryptionService>(),
        Substitute.For<IEmailSender>(),
        Substitute.For<IMapper>());

    [Fact]
    public async Task ChangeVisionPrompt_IsRejectedAndPromptKept()
    {
        await Assert.ThrowsAsync<ProductDefinitionLockedException>(() =>
            WorkspaceService().UpdateAsync(_userId, _workspaceId, new UpdateWorkspaceRequest { VisionPrompt = "new vision" }));

        Assert.Equal("original vision", _workspace.VisionPrompt);
    }

    [Fact]
    public async Task RenameWorkspace_StillAllowed()
    {
        await WorkspaceService().UpdateAsync(_userId, _workspaceId, new UpdateWorkspaceRequest { Name = "renamed" });

        Assert.Equal("renamed", _workspace.Name);
    }
}
