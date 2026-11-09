using AutoMapper;
using NSubstitute;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Domain.Common;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests.Application;

public class LockServiceTests
{
    private readonly IProductDefinitionRepository _repository = Substitute.For<IProductDefinitionRepository>();
    private readonly IWorkspaceMemberRepository _members = Substitute.For<IWorkspaceMemberRepository>();
    private readonly IWorkspaceAccessGuard _guard = Substitute.For<IWorkspaceAccessGuard>();
    private readonly LockService _service;
    private readonly InMemoryAuditTrailRepository _auditEntries = new();
    private readonly AuditTrail _audit;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _workspaceId = Guid.NewGuid();
    private readonly Workspace _workspace = new();
    private readonly Feature _a = Scored("a", 2);
    private readonly Feature _b = Scored("b", 3);
    private readonly ProductDefinition _definition;

    public LockServiceTests()
    {
        _audit = new AuditTrail(_auditEntries);
        _service = new LockService(_repository, _members, _guard, _audit, Substitute.For<IMapper>());

        TestData.DependsOn(_b, _a);
        _definition = TestData.Definition(_a, _b);
        _definition.WorkspaceId = _workspaceId;
        _definition.Status = ProductDefinitionStatus.ReadyForLock;

        _repository.GetByWorkspaceIdAsync(_workspaceId).Returns(_definition);
        _repository.HasSuccessfulAnalysisRunAsync(_definition.Id).Returns(true);
        _guard.GetWorkspaceAsManagerAsync(_userId, _workspaceId).Returns(_workspace);
        GivenMembers(Member(skill: 4));
    }

    private static Feature Scored(string name, int complexity, FeatureOrigin origin = FeatureOrigin.FromAi)
    {
        var feature = TestData.Feature(name, origin);
        feature.Complexity = complexity;
        return feature;
    }

    private static WorkspaceMember Member(int? skill, WorkspaceMemberStatus status = WorkspaceMemberStatus.Active) =>
        new() { UserId = Guid.NewGuid(), Status = status, SkillLevel = skill };

    private void GivenMembers(params WorkspaceMember[] members) =>
        _members.GetByWorkspaceIdAsync(_workspaceId).Returns(members.ToList());

    // ---- successful lock ----

    [Fact]
    public async Task Lock_SealsAVersionOneSnapshotWithEverythingCopied()
    {
        MvpSnapshot? saved = null;
        await _repository.AddSnapshotAsync(Arg.Do<MvpSnapshot>(s => saved = s));

        await _service.LockAsync(_userId, _workspaceId);

        Assert.NotNull(saved);
        Assert.Equal(1, saved!.Version);
        Assert.Equal(_workspaceId, saved.WorkspaceId);
        Assert.Equal(_userId, saved.CreatedByUserId);
        Assert.Equal(1, saved.DevCountAtLock);
        Assert.Equal(64, saved.Hash.Length);

        Assert.Equal(2, saved.Features.Count);
        Assert.All(saved.Features, f => Assert.Equal(SnapshotFeatureStatus.Locked, f.Status));
        Assert.Equal(3, saved.Features.Single(f => f.FeatureId == _b.Id).Complexity);

        var dependency = Assert.Single(saved.Dependencies);
        Assert.Equal(_b.Id, dependency.FeatureId);
        Assert.Equal(_a.Id, dependency.DependsOnFeatureId);
    }

    [Fact]
    public async Task Lock_WritesASnapshotLockedRecordWithTheVersionAndHash()
    {
        MvpSnapshot? saved = null;
        await _repository.AddSnapshotAsync(Arg.Do<MvpSnapshot>(s => saved = s));

        await _service.LockAsync(_userId, _workspaceId);

        var record = Assert.Single(_auditEntries.Entries);
        Assert.Equal(AuditTypes.SnapshotLocked, record.AuditType);
        Assert.Equal(_workspaceId, record.WorkspaceId);
        Assert.Equal(_userId, record.ActorUserId);
        Assert.Contains(saved!.Hash, record.Payload);
        Assert.Contains("\"version\":1", record.Payload);
    }

    [Fact]
    public async Task Lock_BlockedByTheGate_WritesNoRecord()
    {
        _a.Complexity = null;

        await Assert.ThrowsAnyAsync<Exception>(() => _service.LockAsync(_userId, _workspaceId));

        Assert.Empty(_auditEntries.Entries);
    }

    [Fact]
    public async Task Lock_FreezesTheDefinitionAndStampsFirstLockedAt()
    {
        await _service.LockAsync(_userId, _workspaceId);

        Assert.Equal(ProductDefinitionStatus.Locked, _definition.Status);
        Assert.NotNull(_workspace.FirstLockedAt);
        await _repository.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Lock_NeverMovesAnExistingFirstLockedAt()
    {
        var earlier = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _workspace.FirstLockedAt = earlier;

        await _service.LockAsync(_userId, _workspaceId);

        Assert.Equal(earlier, _workspace.FirstLockedAt);
    }

    [Fact]
    public async Task Lock_CountsOnlyActiveMembersAndIgnoresExcludedFeatures()
    {
        _b.Status = FeatureStatus.Excluded;
        _b.Dependencies.Clear();
        GivenMembers(Member(4), Member(2), Member(5, WorkspaceMemberStatus.Invited), Member(5, WorkspaceMemberStatus.Removed));
        MvpSnapshot? saved = null;
        await _repository.AddSnapshotAsync(Arg.Do<MvpSnapshot>(s => saved = s));

        await _service.LockAsync(_userId, _workspaceId);

        Assert.Equal(2, saved!.DevCountAtLock);
        Assert.Single(saved.Features);
        Assert.Empty(saved.Dependencies);
    }

    // ---- blocked lock ----

    private async Task AssertGateFailsAndNothingChanges(Action<LockGateFailedException>? inspect = null)
    {
        var ex = await Assert.ThrowsAsync<LockGateFailedException>(() => _service.LockAsync(_userId, _workspaceId));
        inspect?.Invoke(ex);

        Assert.Equal(ProductDefinitionStatus.ReadyForLock, _definition.Status);
        Assert.Null(_workspace.FirstLockedAt);
        await _repository.DidNotReceive().AddSnapshotAsync(Arg.Any<MvpSnapshot>());
        await _repository.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Lock_BudgetExceeded_IsBlocked()
    {
        _b.Complexity = 5;
        _a.Complexity = 5;
        _definition.Features.Add(Scored("c", 1));

        await AssertGateFailsAndNothingChanges(ex => Assert.True(ex.Gate.BudgetExceeded));
    }

    [Fact]
    public async Task Lock_SkillCeilingViolation_FlagsCommittedFeaturesSoTheyAreNotSuggestedForTheKillList()
    {
        var committed = Scored("committed", 5, FeatureOrigin.FromCommittedList);
        _definition.Features.Add(committed);
        _a.Complexity = 1;
        _b.Complexity = 1;
        GivenMembers(Member(3));

        await AssertGateFailsAndNothingChanges(ex =>
        {
            var violation = Assert.Single(ex.Gate.SkillCeilingViolations);
            Assert.Equal(committed.Id, violation.FeatureId);
            Assert.True(violation.IsCommitted);
        });
    }

    [Fact]
    public async Task Lock_UnscoredFeature_IsBlocked()
    {
        _definition.Features.Add(TestData.Feature("unscored"));

        await AssertGateFailsAndNothingChanges(ex => Assert.Single(ex.Gate.UnscoredFeatures));
    }

    [Fact]
    public async Task Lock_MemberWithoutSkill_IsBlocked()
    {
        GivenMembers(Member(4), Member(null));

        await AssertGateFailsAndNothingChanges(ex => Assert.Equal(1, ex.Gate.MembersWithoutSkill));
    }

    [Fact]
    public async Task Lock_DependencyOnExcludedFeature_IsBlocked()
    {
        var cut = TestData.Feature("cut", status: FeatureStatus.Excluded);
        _definition.Features.Add(cut);
        TestData.DependsOn(_a, cut);

        await AssertGateFailsAndNothingChanges(ex => Assert.Single(ex.Gate.BrokenDependencies));
    }

    // ---- preconditions ----

    [Fact]
    public async Task Lock_WhileDraft_IsRejected()
    {
        _definition.Status = ProductDefinitionStatus.Draft;

        await Assert.ThrowsAsync<ProductDefinitionNotReadyForLockException>(() =>
            _service.LockAsync(_userId, _workspaceId));
    }

    [Fact]
    public async Task Lock_WhenAlreadyLocked_IsRejected()
    {
        _definition.Status = ProductDefinitionStatus.Locked;

        await Assert.ThrowsAsync<ProductDefinitionLockedException>(() => _service.LockAsync(_userId, _workspaceId));
        await _repository.DidNotReceive().AddSnapshotAsync(Arg.Any<MvpSnapshot>());
    }

    [Fact]
    public async Task Lock_NeverAnalyzed_IsRejected()
    {
        _repository.HasSuccessfulAnalysisRunAsync(_definition.Id).Returns(false);

        await Assert.ThrowsAsync<ProductDefinitionNotAnalyzedException>(() => _service.LockAsync(_userId, _workspaceId));
    }

    [Fact]
    public async Task Lock_NonManager_IsRejected()
    {
        _guard.GetWorkspaceAsManagerAsync(_userId, _workspaceId)
            .Returns<Workspace>(_ => throw new WorkspacePermissionDeniedException());

        await Assert.ThrowsAsync<WorkspacePermissionDeniedException>(() => _service.LockAsync(_userId, _workspaceId));
        await _repository.DidNotReceive().SaveChangesAsync();
    }

    // ---- check and read ----

    [Fact]
    public async Task Check_ReportsTheGateWithoutLocking()
    {
        _b.Complexity = null;

        var gate = await _service.CheckAsync(_userId, _workspaceId);

        Assert.False(gate.IsPassed);
        Assert.Single(gate.UnscoredFeatures);
        Assert.Equal(ProductDefinitionStatus.ReadyForLock, _definition.Status);
        await _repository.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Check_PassingGate_DoesNotLockEither()
    {
        var gate = await _service.CheckAsync(_userId, _workspaceId);

        Assert.True(gate.IsPassed);
        Assert.Equal(ProductDefinitionStatus.ReadyForLock, _definition.Status);
    }

    [Fact]
    public async Task GetLatestSnapshot_NeverLocked_IsNotFound()
    {
        _repository.GetLatestSnapshotAsync(_workspaceId).Returns((MvpSnapshot?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetLatestSnapshotAsync(_userId, _workspaceId));
    }
}
