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

public class FeatureReviewServiceTests
{
    private readonly IProductDefinitionRepository _repository = Substitute.For<IProductDefinitionRepository>();
    private readonly IWorkspaceAccessGuard _guard = Substitute.For<IWorkspaceAccessGuard>();
    private readonly FeatureReviewService _service;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _workspaceId = Guid.NewGuid();

    public FeatureReviewServiceTests()
    {
        _service = new FeatureReviewService(_repository, _guard, Substitute.For<IMapper>());
    }

    private ProductDefinition Given(params Feature[] features)
    {
        var definition = TestData.Definition(features);
        definition.Status = ProductDefinitionStatus.ReadyForLock;
        _repository.GetByWorkspaceIdAsync(_workspaceId).Returns(definition);
        _repository.HasSuccessfulAnalysisRunAsync(definition.Id).Returns(true);
        return definition;
    }

    private static Feature Pending(string name, FeatureOrigin origin = FeatureOrigin.FromCustomerMentioned) =>
        TestData.Feature(name, origin, FeatureStatus.PendingConfirmation);

    // ---- access and preconditions ----

    [Fact]
    public async Task Review_BeforeFirstSuccessfulAnalysis_IsRejected()
    {
        var feature = Pending("a");
        var definition = TestData.Definition(feature);
        _repository.GetByWorkspaceIdAsync(_workspaceId).Returns(definition);
        _repository.HasSuccessfulAnalysisRunAsync(definition.Id).Returns(false);

        await Assert.ThrowsAsync<ProductDefinitionNotAnalyzedException>(() =>
            _service.ChangeStatusAsync(_userId, _workspaceId, feature.Id,
                new UpdateFeatureStatusRequest { Status = FeatureStatus.Included }));
    }

    [Fact]
    public async Task Review_NonManager_IsRejectedBeforeAnythingChanges()
    {
        var feature = Pending("a");
        Given(feature);
        _guard.GetWorkspaceAsManagerAsync(_userId, _workspaceId).Returns<Workspace>(_ => throw new WorkspacePermissionDeniedException());

        await Assert.ThrowsAsync<WorkspacePermissionDeniedException>(() =>
            _service.ChangeStatusAsync(_userId, _workspaceId, feature.Id,
                new UpdateFeatureStatusRequest { Status = FeatureStatus.Included }));

        Assert.Equal(FeatureStatus.PendingConfirmation, feature.Status);
        await _repository.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Review_UnknownFeature_IsNotFound()
    {
        Given(Pending("a"));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ChangeStatusAsync(_userId, _workspaceId, Guid.NewGuid(),
                new UpdateFeatureStatusRequest { Status = FeatureStatus.Included }));
    }

    // ---- status changes (steps 5, 7, 8) ----

    [Fact]
    public async Task ChangeStatus_IncludeMentionedFeature_MarksDecidedAndSendsDefinitionBackToDraft()
    {
        var feature = Pending("a");
        var definition = Given(feature);

        await _service.ChangeStatusAsync(_userId, _workspaceId, feature.Id,
            new UpdateFeatureStatusRequest { Status = FeatureStatus.Included });

        Assert.Equal(FeatureStatus.Included, feature.Status);
        Assert.True(feature.DevDecided);
        Assert.Equal(ProductDefinitionStatus.Draft, definition.Status);
        await _repository.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task ChangeStatus_ExcludeCommittedFeature_RequiresChangeRequest()
    {
        var committed = Pending("c", FeatureOrigin.FromCommittedList);
        Given(committed);

        await Assert.ThrowsAsync<CommittedFeatureChangeRequiredException>(() =>
            _service.ChangeStatusAsync(_userId, _workspaceId, committed.Id,
                new UpdateFeatureStatusRequest { Status = FeatureStatus.Excluded }));

        Assert.Equal(FeatureStatus.PendingConfirmation, committed.Status);
    }

    [Fact]
    public async Task ChangeStatus_IncludeCommittedFeature_IsFreeAndNotAuditedTheFirstTime()
    {
        var committed = Pending("c", FeatureOrigin.FromCommittedList);
        Given(committed);

        await _service.ChangeStatusAsync(_userId, _workspaceId, committed.Id,
            new UpdateFeatureStatusRequest { Status = FeatureStatus.Included });

        Assert.Equal(FeatureStatus.Included, committed.Status);
        await _repository.DidNotReceive().AddChangeLogAsync(Arg.Any<FeatureChangeLog>());
    }

    [Fact]
    public async Task ChangeStatus_ReIncludeExcludedCommittedFeature_IsAudited()
    {
        var committed = TestData.Feature("c", FeatureOrigin.FromCommittedList, FeatureStatus.Excluded);
        Given(committed);

        await _service.ChangeStatusAsync(_userId, _workspaceId, committed.Id,
            new UpdateFeatureStatusRequest { Status = FeatureStatus.Included });

        await _repository.Received(1).AddChangeLogAsync(Arg.Is<FeatureChangeLog>(log =>
            log.FeatureId == committed.Id
            && log.Action == FeatureChangeAction.Include
            && log.PerformedByUserId == _userId));
    }

    // ---- excluding a feature other features depend on (step 5) ----

    [Fact]
    public async Task Exclude_FeatureWithIncludedDependents_AsksForResolution()
    {
        var prerequisite = TestData.Feature("prerequisite");
        var dependent = TestData.Feature("dependent");
        TestData.DependsOn(dependent, prerequisite);
        Given(prerequisite, dependent);

        var ex = await Assert.ThrowsAsync<FeatureHasDependentsException>(() =>
            _service.ChangeStatusAsync(_userId, _workspaceId, prerequisite.Id,
                new UpdateFeatureStatusRequest { Status = FeatureStatus.Excluded }));

        Assert.Equal(dependent.Id, Assert.Single(ex.DependentFeatures).Id);
        Assert.Equal(FeatureStatus.Included, prerequisite.Status);
    }

    [Fact]
    public async Task Exclude_WithRemoveLinks_DropsTheLinksAndKeepsDependentsIncluded()
    {
        var prerequisite = TestData.Feature("prerequisite");
        var dependent = TestData.Feature("dependent");
        TestData.DependsOn(dependent, prerequisite);
        Given(prerequisite, dependent);

        await _service.ChangeStatusAsync(_userId, _workspaceId, prerequisite.Id,
            new UpdateFeatureStatusRequest
            {
                Status = FeatureStatus.Excluded,
                DependentsResolution = DependentsResolution.RemoveLinks
            });

        Assert.Equal(FeatureStatus.Excluded, prerequisite.Status);
        Assert.Equal(FeatureStatus.Included, dependent.Status);
        Assert.True(dependent.DevDecided);
        _repository.Received(1).RemoveDependency(Arg.Is<FeatureDependency>(d => d.FeatureId == dependent.Id));
    }

    [Fact]
    public async Task Exclude_WithExcludeDependents_CascadesThroughIndirectDependents()
    {
        var root = TestData.Feature("root");
        var middle = TestData.Feature("middle");
        var leaf = TestData.Feature("leaf");
        TestData.DependsOn(middle, root);
        TestData.DependsOn(leaf, middle);
        Given(root, middle, leaf);

        await _service.ChangeStatusAsync(_userId, _workspaceId, root.Id,
            new UpdateFeatureStatusRequest
            {
                Status = FeatureStatus.Excluded,
                DependentsResolution = DependentsResolution.ExcludeDependents
            });

        Assert.All(new[] { root, middle, leaf }, f => Assert.Equal(FeatureStatus.Excluded, f.Status));
    }

    [Fact]
    public async Task Exclude_WithExcludeDependents_BlockedWhenChainContainsCommittedFeature()
    {
        var root = TestData.Feature("root");
        var committedDependent = TestData.Feature("committed", FeatureOrigin.FromCommittedList);
        TestData.DependsOn(committedDependent, root);
        Given(root, committedDependent);

        await Assert.ThrowsAsync<CommittedFeatureChangeRequiredException>(() =>
            _service.ChangeStatusAsync(_userId, _workspaceId, root.Id,
                new UpdateFeatureStatusRequest
                {
                    Status = FeatureStatus.Excluded,
                    DependentsResolution = DependentsResolution.ExcludeDependents
                }));

        Assert.Equal(FeatureStatus.Included, root.Status);
        Assert.Equal(FeatureStatus.Included, committedDependent.Status);
    }

    [Fact]
    public async Task Exclude_IgnoresDependentsThatAreAlreadyExcluded()
    {
        var prerequisite = TestData.Feature("prerequisite");
        var excludedDependent = TestData.Feature("dependent", status: FeatureStatus.Excluded);
        TestData.DependsOn(excludedDependent, prerequisite);
        Given(prerequisite, excludedDependent);

        await _service.ChangeStatusAsync(_userId, _workspaceId, prerequisite.Id,
            new UpdateFeatureStatusRequest { Status = FeatureStatus.Excluded });

        Assert.Equal(FeatureStatus.Excluded, prerequisite.Status);
    }

    // ---- editing (step 5) ----

    [Fact]
    public async Task Edit_AiFeature_ChangesOnlyTheSentFieldsAndMarksDecided()
    {
        var feature = TestData.Feature("old name");
        feature.Scope = "old scope";
        Given(feature);

        await _service.EditAsync(_userId, _workspaceId, feature.Id, new UpdateFeatureRequest { Name = "new name" });

        Assert.Equal("new name", feature.Name);
        Assert.Equal("old name description", feature.Description);
        Assert.Equal("old scope", feature.Scope);
        Assert.True(feature.DevDecided);
    }

    [Fact]
    public async Task Edit_CommittedFeature_RequiresChangeRequest()
    {
        var committed = TestData.Feature("c", FeatureOrigin.FromCommittedList);
        Given(committed);

        await Assert.ThrowsAsync<CommittedFeatureChangeRequiredException>(() =>
            _service.EditAsync(_userId, _workspaceId, committed.Id, new UpdateFeatureRequest { Name = "x" }));

        Assert.Equal("c", committed.Name);
    }

    // ---- change requests on committed features (step 8) ----

    [Fact]
    public async Task ChangeRequest_Edit_AppliesChangeAndWritesAuditEntry()
    {
        var committed = TestData.Feature("Online Payment", FeatureOrigin.FromCommittedList, FeatureStatus.Included);
        var definition = Given(committed);

        await _service.SubmitChangeRequestAsync(_userId, _workspaceId, committed.Id, new CommittedFeatureChangeRequest
        {
            Action = FeatureChangeAction.Edit,
            Name = "Online Payment (VNPay + MoMo)",
            Reason = "Customer asked for MoMo",
            CustomerNotifiedConfirmed = true
        });

        Assert.Equal("Online Payment (VNPay + MoMo)", committed.Name);
        Assert.True(committed.DevDecided);
        Assert.Equal(ProductDefinitionStatus.Draft, definition.Status);
        await _repository.Received(1).AddChangeLogAsync(Arg.Is<FeatureChangeLog>(log =>
            log.Action == FeatureChangeAction.Edit
            && log.Reason == "Customer asked for MoMo"
            && log.CustomerNotifiedConfirmed
            && log.OldContent!.Contains("Online Payment")
            && log.NewContent!.Contains("MoMo")));
    }

    [Fact]
    public async Task ChangeRequest_Exclude_ExcludesAndAudits()
    {
        var committed = Pending("c", FeatureOrigin.FromCommittedList);
        Given(committed);

        await _service.SubmitChangeRequestAsync(_userId, _workspaceId, committed.Id, new CommittedFeatureChangeRequest
        {
            Action = FeatureChangeAction.Exclude,
            Reason = "Out of budget",
            CustomerNotifiedConfirmed = true
        });

        Assert.Equal(FeatureStatus.Excluded, committed.Status);
        await _repository.Received(1).AddChangeLogAsync(Arg.Is<FeatureChangeLog>(log => log.Action == FeatureChangeAction.Exclude));
    }

    [Fact]
    public async Task ChangeRequest_ExcludeAlreadyExcluded_IsRejected()
    {
        var committed = TestData.Feature("c", FeatureOrigin.FromCommittedList, FeatureStatus.Excluded);
        Given(committed);

        await Assert.ThrowsAsync<FeatureAlreadyExcludedException>(() =>
            _service.SubmitChangeRequestAsync(_userId, _workspaceId, committed.Id, new CommittedFeatureChangeRequest
            {
                Action = FeatureChangeAction.Exclude,
                Reason = "again",
                CustomerNotifiedConfirmed = true
            }));
    }

    [Fact]
    public async Task ChangeRequest_OnNonCommittedFeature_IsRejected()
    {
        var feature = TestData.Feature("a");
        Given(feature);

        await Assert.ThrowsAsync<FeatureNotCommittedException>(() =>
            _service.SubmitChangeRequestAsync(_userId, _workspaceId, feature.Id, new CommittedFeatureChangeRequest
            {
                Action = FeatureChangeAction.Edit,
                Name = "x",
                Reason = "r",
                CustomerNotifiedConfirmed = true
            }));
    }

    // ---- duplicates (steps 5, 6) ----

    private (Feature Mentioned, Feature Committed) GivenDuplicatePair()
    {
        var committed = TestData.Feature("Online Payment", FeatureOrigin.FromCommittedList, FeatureStatus.PendingConfirmation, position: 1);
        var mentioned = TestData.Feature("Pay online", FeatureOrigin.FromCustomerMentioned, FeatureStatus.PendingConfirmation, position: 0);
        mentioned.PossibleDuplicate = true;
        mentioned.DuplicateOfId = committed.Id;
        mentioned.DuplicateReason = "same function";
        Given(mentioned, committed);
        return (mentioned, committed);
    }

    [Fact]
    public async Task Merge_KeepsCommittedFeatureUnionsSourcesAndAudits()
    {
        var (mentioned, committed) = GivenDuplicatePair();

        await _service.MergeAsync(_userId, _workspaceId, mentioned.Id);

        Assert.Equal("Online Payment", committed.Name);
        Assert.Equal(FeatureOrigin.FromCommittedList, committed.Origin);
        Assert.Contains(FeatureOrigin.FromCustomerMentioned, committed.Sources);
        Assert.Contains(FeatureOrigin.FromCommittedList, committed.Sources);
        Assert.True(committed.DevDecided);
        _repository.Received(1).RemoveFeature(mentioned);
        await _repository.Received(1).AddChangeLogAsync(Arg.Is<FeatureChangeLog>(log =>
            log.Action == FeatureChangeAction.Merge && log.FeatureId == committed.Id));
    }

    [Fact]
    public async Task Merge_WithoutCommittedFeature_DoesNotWriteAuditEntry()
    {
        var first = TestData.Feature("first", FeatureOrigin.FromCustomerMentioned, position: 0);
        var second = TestData.Feature("second", FeatureOrigin.FromAi, position: 1);
        second.PossibleDuplicate = true;
        second.DuplicateOfId = first.Id;
        Given(first, second);

        await _service.MergeAsync(_userId, _workspaceId, second.Id);

        // The higher origin survives and keeps its content.
        _repository.Received(1).RemoveFeature(second);
        Assert.Equal("first", first.Name);
        Assert.Equal(FeatureOrigin.FromCustomerMentioned, first.Origin);
        await _repository.DidNotReceive().AddChangeLogAsync(Arg.Any<FeatureChangeLog>());
    }

    [Fact]
    public async Task Merge_MovesDependenciesOntoTheSurvivor()
    {
        var (mentioned, committed) = GivenDuplicatePair();
        var other = TestData.Feature("other");
        TestData.DependsOn(other, mentioned);
        var definition = await _repository.GetByWorkspaceIdAsync(_workspaceId);
        definition!.Features.Add(other);

        await _service.MergeAsync(_userId, _workspaceId, mentioned.Id);

        await _repository.Received(1).AddDependencyAsync(Arg.Is<FeatureDependency>(d =>
            d.FeatureId == other.Id && d.DependsOnFeatureId == committed.Id));
        _repository.Received(1).RemoveDependency(Arg.Is<FeatureDependency>(d => d.DependsOnFeatureId == mentioned.Id));
    }

    [Fact]
    public async Task Merge_FeatureThatIsNotFlagged_IsRejected()
    {
        var feature = TestData.Feature("a");
        Given(feature);

        await Assert.ThrowsAsync<FeatureNotFlaggedAsDuplicateException>(() =>
            _service.MergeAsync(_userId, _workspaceId, feature.Id));
    }

    [Fact]
    public async Task KeepSeparate_ClearsFlagAndMarksBothFeaturesDecided()
    {
        var (mentioned, committed) = GivenDuplicatePair();

        await _service.KeepSeparateAsync(_userId, _workspaceId, mentioned.Id);

        Assert.False(mentioned.PossibleDuplicate);
        Assert.Null(mentioned.DuplicateOfId);
        Assert.True(mentioned.DevDecided);
        Assert.True(committed.DevDecided);
        _repository.DidNotReceive().RemoveFeature(Arg.Any<Feature>());
    }

    // ---- persona conflict (step 5) ----

    [Fact]
    public async Task AcceptPersonaConflict_ClearsFlagAndMarksDecided()
    {
        var feature = TestData.Feature("Payroll", FeatureOrigin.FromCommittedList);
        feature.PersonaConflict = true;
        feature.PersonaConflictReason = "not for patients";
        Given(feature);

        await _service.AcceptPersonaConflictAsync(_userId, _workspaceId, feature.Id);

        Assert.False(feature.PersonaConflict);
        Assert.Null(feature.PersonaConflictReason);
        Assert.True(feature.DevDecided);
    }

    [Fact]
    public async Task AcceptPersonaConflict_OnUnflaggedFeature_IsRejected()
    {
        var feature = TestData.Feature("a");
        Given(feature);

        await Assert.ThrowsAsync<FeatureNotFlaggedAsPersonaConflictException>(() =>
            _service.AcceptPersonaConflictAsync(_userId, _workspaceId, feature.Id));
    }

    // ---- dependencies (steps 5, 5b) ----

    [Fact]
    public async Task AddDependency_Valid_AddsLinkAndMarksBothEndsDecided()
    {
        var a = TestData.Feature("a");
        var b = TestData.Feature("b");
        Given(a, b);

        await _service.AddDependencyAsync(_userId, _workspaceId, b.Id,
            new AddFeatureDependencyRequest { DependsOnFeatureId = a.Id, Reason = "needs a" });

        await _repository.Received(1).AddDependencyAsync(Arg.Is<FeatureDependency>(d =>
            d.FeatureId == b.Id && d.DependsOnFeatureId == a.Id && d.Reason == "needs a"));
        Assert.True(a.DevDecided);
        Assert.True(b.DevDecided);
    }

    [Fact]
    public async Task AddDependency_OnItself_IsRejected()
    {
        var a = TestData.Feature("a");
        Given(a);

        await Assert.ThrowsAsync<FeatureSelfDependencyException>(() =>
            _service.AddDependencyAsync(_userId, _workspaceId, a.Id, new AddFeatureDependencyRequest { DependsOnFeatureId = a.Id }));
    }

    [Fact]
    public async Task AddDependency_Duplicate_IsRejected()
    {
        var a = TestData.Feature("a");
        var b = TestData.Feature("b");
        TestData.DependsOn(b, a);
        Given(a, b);

        await Assert.ThrowsAsync<DependencyAlreadyExistsException>(() =>
            _service.AddDependencyAsync(_userId, _workspaceId, b.Id, new AddFeatureDependencyRequest { DependsOnFeatureId = a.Id }));
    }

    [Fact]
    public async Task AddDependency_IncludedOnExcluded_IsRejected()
    {
        var excluded = TestData.Feature("excluded", status: FeatureStatus.Excluded);
        var included = TestData.Feature("included");
        Given(excluded, included);

        await Assert.ThrowsAsync<DependencyOnExcludedFeatureException>(() =>
            _service.AddDependencyAsync(_userId, _workspaceId, included.Id,
                new AddFeatureDependencyRequest { DependsOnFeatureId = excluded.Id }));
    }

    [Fact]
    public async Task AddDependency_ClosingACycle_IsRejectedWithTheCycle()
    {
        var a = TestData.Feature("a");
        var b = TestData.Feature("b");
        TestData.DependsOn(b, a);
        Given(a, b);

        var ex = await Assert.ThrowsAsync<DependencyCreatesCycleException>(() =>
            _service.AddDependencyAsync(_userId, _workspaceId, a.Id, new AddFeatureDependencyRequest { DependsOnFeatureId = b.Id }));

        Assert.Equal(3, ex.Cycle.Count);
        await _repository.DidNotReceive().AddDependencyAsync(Arg.Any<FeatureDependency>());
    }

    [Fact]
    public async Task AddDependency_UnknownPrerequisite_IsNotFound()
    {
        var a = TestData.Feature("a");
        Given(a);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.AddDependencyAsync(_userId, _workspaceId, a.Id, new AddFeatureDependencyRequest { DependsOnFeatureId = Guid.NewGuid() }));
    }

    [Fact]
    public async Task RemoveDependency_RemovesLinkAndMarksBothEndsDecided()
    {
        var a = TestData.Feature("a");
        var b = TestData.Feature("b");
        TestData.DependsOn(b, a);
        Given(a, b);

        await _service.RemoveDependencyAsync(_userId, _workspaceId, b.Id, a.Id);

        _repository.Received(1).RemoveDependency(Arg.Is<FeatureDependency>(d => d.DependsOnFeatureId == a.Id));
        Assert.True(a.DevDecided);
        Assert.True(b.DevDecided);
    }

    [Fact]
    public async Task RemoveDependency_NoSuchLink_IsNotFound()
    {
        var a = TestData.Feature("a");
        var b = TestData.Feature("b");
        Given(a, b);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.RemoveDependencyAsync(_userId, _workspaceId, b.Id, a.Id));
    }
}
