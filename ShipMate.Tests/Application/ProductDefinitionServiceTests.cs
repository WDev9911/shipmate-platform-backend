using AutoMapper;
using NSubstitute;
using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests.Application;

public class ProductDefinitionServiceTests
{
    private readonly IProductDefinitionRepository _repository = Substitute.For<IProductDefinitionRepository>();
    private readonly IWorkspaceAccessGuard _guard = Substitute.For<IWorkspaceAccessGuard>();
    private readonly ProductDefinitionService _service;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _workspaceId = Guid.NewGuid();

    public ProductDefinitionServiceTests()
    {
        _service = new ProductDefinitionService(_repository, _guard, Substitute.For<IMapper>());
    }

    private ProductDefinition Given(bool analyzed, params Feature[] features)
    {
        var definition = TestData.Definition(features);
        _repository.GetByWorkspaceIdAsync(_workspaceId).Returns(definition);
        _repository.HasSuccessfulAnalysisRunAsync(definition.Id).Returns(analyzed);
        return definition;
    }

    // ---- READY_FOR_LOCK (step 7) ----

    [Fact]
    public async Task MarkReady_NoProductDefinition_IsRejected()
    {
        _repository.GetByWorkspaceIdAsync(_workspaceId).Returns((ProductDefinition?)null);

        await Assert.ThrowsAsync<ProductDefinitionNotAnalyzedException>(() =>
            _service.MarkReadyForLockAsync(_userId, _workspaceId));
    }

    [Fact]
    public async Task MarkReady_NeverAnalyzed_IsRejected()
    {
        Given(analyzed: false, TestData.Feature("a"));

        await Assert.ThrowsAsync<ProductDefinitionNotAnalyzedException>(() =>
            _service.MarkReadyForLockAsync(_userId, _workspaceId));
    }

    [Fact]
    public async Task MarkReady_WithPendingFeature_IsRejectedAndStaysDraft()
    {
        var definition = Given(true, TestData.Feature("a", FeatureOrigin.FromCustomerMentioned, FeatureStatus.PendingConfirmation));

        await Assert.ThrowsAsync<ProductDefinitionNotReadyException>(() =>
            _service.MarkReadyForLockAsync(_userId, _workspaceId));

        Assert.Equal(ProductDefinitionStatus.Draft, definition.Status);
        await _repository.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task MarkReady_WithCycle_IsRejected()
    {
        var a = TestData.Feature("a");
        var b = TestData.Feature("b");
        TestData.DependsOn(a, b);
        TestData.DependsOn(b, a);
        Given(true, a, b);

        await Assert.ThrowsAsync<ProductDefinitionNotReadyException>(() =>
            _service.MarkReadyForLockAsync(_userId, _workspaceId));
    }

    [Fact]
    public async Task MarkReady_WhenEverythingIsResolved_MovesToReadyForLock()
    {
        var definition = Given(true, TestData.Feature("a"));

        await _service.MarkReadyForLockAsync(_userId, _workspaceId);

        Assert.Equal(ProductDefinitionStatus.ReadyForLock, definition.Status);
        await _repository.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task MarkReady_WhenAlreadyReady_DoesNotSaveAgain()
    {
        var definition = Given(true, TestData.Feature("a"));
        definition.Status = ProductDefinitionStatus.ReadyForLock;

        await _service.MarkReadyForLockAsync(_userId, _workspaceId);

        await _repository.DidNotReceive().SaveChangesAsync();
    }

    // ---- report (step 4) ----

    [Fact]
    public async Task Report_BeforeAnalysis_IsRejected()
    {
        Given(analyzed: false);

        await Assert.ThrowsAsync<ProductDefinitionNotAnalyzedException>(() =>
            _service.GetReportAsync(_userId, _workspaceId));
    }

    [Fact]
    public async Task Report_CountsCoreAndSupportingFeaturesSeparately()
    {
        Given(true,
            TestData.Feature("p1"),
            TestData.Feature("p2"),
            TestData.Feature("p3"),
            TestData.Feature("p4"),
            TestData.Feature("p5"),
            TestData.Feature("p6"),
            TestData.Feature("s1", role: FeatureRole.Supporting),
            TestData.Feature("cut", status: FeatureStatus.Excluded, verdict: AiVerdict.NotRequired));

        var report = await _service.GetReportAsync(_userId, _workspaceId);

        Assert.Equal(6, report.CoreFeatures.PrimaryCount);
        Assert.Equal(1, report.CoreFeatures.SupportingCount);
        Assert.Equal(3, report.CoreFeatures.RecommendedMin);
        Assert.Equal(5, report.CoreFeatures.RecommendedMax);
        Assert.True(report.CoreFeatures.ExceedsRecommendation);
    }

    // ---- committed features entered before/after analysis (step 1) ----

    [Fact]
    public async Task AddCommittedFeature_NoDefinitionYet_CreatesOneAndAddsFeaturePending()
    {
        _repository.GetByWorkspaceIdAsync(_workspaceId).Returns((ProductDefinition?)null);

        await _service.AddCommittedFeatureAsync(_userId, _workspaceId,
            new CreateCommittedFeatureRequest { Name = "Online Payment", Description = "VNPay" });

        await _repository.Received(1).AddAsync(Arg.Is<ProductDefinition>(d => d.WorkspaceId == _workspaceId));
        await _repository.Received(1).AddFeatureAsync(Arg.Is<Feature>(f =>
            f.Name == "Online Payment"
            && f.Origin == FeatureOrigin.FromCommittedList
            && f.Status == FeatureStatus.PendingConfirmation
            && f.Role == null
            && f.Position == 0));
    }

    [Fact]
    public async Task AddCommittedFeature_AppendsAfterExistingFeaturesAndSendsDefinitionBackToDraft()
    {
        var definition = Given(true, TestData.Feature("a", position: 3));
        definition.Status = ProductDefinitionStatus.ReadyForLock;

        await _service.AddCommittedFeatureAsync(_userId, _workspaceId,
            new CreateCommittedFeatureRequest { Name = "c", Description = "d" });

        await _repository.Received(1).AddFeatureAsync(Arg.Is<Feature>(f => f.Position == 4));
        Assert.Equal(ProductDefinitionStatus.Draft, definition.Status);
    }

    [Fact]
    public async Task UpdateCommittedFeature_AfterFirstAnalysis_MustGoThroughChangeRequest()
    {
        var committed = TestData.Feature("c", FeatureOrigin.FromCommittedList);
        Given(true, committed);

        await Assert.ThrowsAsync<CommittedFeatureLockedException>(() =>
            _service.UpdateCommittedFeatureAsync(_userId, _workspaceId, committed.Id,
                new UpdateCommittedFeatureRequest { Name = "x" }));

        Assert.Equal("c", committed.Name);
    }

    [Fact]
    public async Task UpdateCommittedFeature_BeforeFirstAnalysis_EditsFreely()
    {
        var committed = TestData.Feature("c", FeatureOrigin.FromCommittedList);
        Given(false, committed);

        await _service.UpdateCommittedFeatureAsync(_userId, _workspaceId, committed.Id,
            new UpdateCommittedFeatureRequest { Name = "renamed" });

        Assert.Equal("renamed", committed.Name);
    }

    [Fact]
    public async Task DeleteCommittedFeature_BeforeFirstAnalysis_RemovesIt()
    {
        var committed = TestData.Feature("c", FeatureOrigin.FromCommittedList);
        Given(false, committed);

        await _service.DeleteCommittedFeatureAsync(_userId, _workspaceId, committed.Id);

        _repository.Received(1).RemoveFeature(committed);
    }

    [Fact]
    public async Task DeleteCommittedFeature_AfterFirstAnalysis_IsRejected()
    {
        var committed = TestData.Feature("c", FeatureOrigin.FromCommittedList);
        Given(true, committed);

        await Assert.ThrowsAsync<CommittedFeatureLockedException>(() =>
            _service.DeleteCommittedFeatureAsync(_userId, _workspaceId, committed.Id));

        _repository.DidNotReceive().RemoveFeature(Arg.Any<Feature>());
    }

    [Fact]
    public async Task DeleteCommittedFeature_NonCommittedFeature_IsNotFound()
    {
        var aiFeature = TestData.Feature("a");
        Given(false, aiFeature);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.DeleteCommittedFeatureAsync(_userId, _workspaceId, aiFeature.Id));
    }

    // ---- project constraints (optional input) ----

    [Fact]
    public async Task UpdateConstraints_StoresTrimmedPartsAndDropsBlankOnes()
    {
        var definition = Given(false);

        await _service.UpdateConstraintsAsync(_userId, _workspaceId, new UpdateProjectConstraintsRequest
        {
            Customer = "  HIPAA only  ",
            Company = "   ",
            Team = null
        });

        Assert.NotNull(definition.Constraints);
        Assert.Equal("HIPAA only", definition.Constraints!.Customer);
        Assert.Null(definition.Constraints.Company);
        Assert.Null(definition.Constraints.Team);
    }

    [Fact]
    public async Task UpdateConstraints_AllBlank_ClearsConstraints()
    {
        var definition = Given(false);
        definition.Constraints = new() { Team = "old rule" };

        await _service.UpdateConstraintsAsync(_userId, _workspaceId, new UpdateProjectConstraintsRequest());

        Assert.Null(definition.Constraints);
    }

    [Fact]
    public async Task UpdateConstraints_NoDefinitionYet_CreatesOne()
    {
        _repository.GetByWorkspaceIdAsync(_workspaceId).Returns((ProductDefinition?)null);

        await _service.UpdateConstraintsAsync(_userId, _workspaceId, new UpdateProjectConstraintsRequest { Team = "rule" });

        await _repository.Received(1).AddAsync(Arg.Is<ProductDefinition>(d =>
            d.WorkspaceId == _workspaceId && d.Constraints != null && d.Constraints.Team == "rule"));
        await _repository.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task UpdateConstraints_WhileReadyForLock_ReturnsToDraft()
    {
        var definition = Given(true, TestData.Feature("a"));
        definition.Status = ProductDefinitionStatus.ReadyForLock;

        await _service.UpdateConstraintsAsync(_userId, _workspaceId, new UpdateProjectConstraintsRequest { Company = "handbook" });

        Assert.Equal(ProductDefinitionStatus.Draft, definition.Status);
    }

    [Fact]
    public async Task UpdateConstraints_NonManager_IsRejectedAndNothingSaved()
    {
        _guard.GetWorkspaceAsManagerAsync(_userId, _workspaceId).Returns<Workspace>(_ => throw new WorkspacePermissionDeniedException());

        await Assert.ThrowsAsync<WorkspacePermissionDeniedException>(() =>
            _service.UpdateConstraintsAsync(_userId, _workspaceId, new UpdateProjectConstraintsRequest { Team = "x" }));

        await _repository.DidNotReceive().SaveChangesAsync();
    }
}
