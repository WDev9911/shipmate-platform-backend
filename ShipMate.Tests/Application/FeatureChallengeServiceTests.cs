using System.Text.Json;
using AutoMapper;
using NSubstitute;
using ShipMate.Application.DTOs.Ai;
using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests.Application;

// The AI gateway is faked: it answers with a fixed score for whatever feature it is asked about.
public class FeatureChallengeServiceTests
{
    private readonly IFeatureChallengeRepository _challenges = Substitute.For<IFeatureChallengeRepository>();
    private readonly IProductDefinitionRepository _definitions = Substitute.For<IProductDefinitionRepository>();
    private readonly IWorkspaceMemberRepository _members = Substitute.For<IWorkspaceMemberRepository>();
    private readonly IFeatureAssignmentRepository _assignments = Substitute.For<IFeatureAssignmentRepository>();
    private readonly IWorkspaceAccessGuard _guard = Substitute.For<IWorkspaceAccessGuard>();
    private readonly IAiGateway _ai = Substitute.For<IAiGateway>();
    private readonly FeatureChallengeService _service;
    private readonly Guid _manager = Guid.NewGuid();
    private readonly Guid _developer = Guid.NewGuid();
    private readonly Guid _workspaceId = Guid.NewGuid();
    private readonly SnapshotFeature _a = F("a", 2);
    private readonly SnapshotFeature _b = F("b", 3);
    private readonly SnapshotFeature _committed = F("committed", 1, FeatureOrigin.FromCommittedList);
    private readonly MvpSnapshot _snapshot;
    private int _aiScore = 3;

    public FeatureChallengeServiceTests()
    {
        var mapper = Substitute.For<IMapper>();
        mapper.Map<FeatureChallengeDto>(Arg.Any<object>()).Returns(call =>
        {
            var c = (FeatureChallenge)call.Args()[0];
            return new FeatureChallengeDto
            {
                Id = c.Id,
                Status = c.Status,
                ProposedComplexity = c.ProposedComplexity,
                Complexity = c.Complexity,
                Action = c.Action,
                ResultingSnapshotVersion = c.ResultingSnapshotVersion,
                RejectionReason = c.RejectionReason
            };
        });

        _service = new FeatureChallengeService(_challenges, _definitions, _assignments, _members, _guard, _ai, mapper);

        _snapshot = new MvpSnapshot { WorkspaceId = _workspaceId, Version = 1, DevCountAtLock = 1 };
        _snapshot.Features.Add(_a);
        _snapshot.Features.Add(_b);
        _snapshot.Features.Add(_committed);
        _definitions.GetLatestSnapshotAsync(_workspaceId).Returns(_snapshot);
        _assignments.GetByWorkspaceIdAsync(_workspaceId).Returns(new List<FeatureAssignment>());
        _definitions.GetByWorkspaceIdAsync(_workspaceId).Returns(new ProductDefinition { Problem = "p", Solution = "s" });

        GivenMembers(Member(_manager, WorkspaceMemberRole.Manager, skill: 5));

        _guard.GetWorkspaceAsManagerAsync(_manager, _workspaceId).Returns(new Workspace());
        _guard.GetWorkspaceAsManagerAsync(_developer, _workspaceId)
            .Returns<Workspace>(_ => throw new WorkspacePermissionDeniedException());
        _guard.GetWorkspaceAsMemberAsync(Arg.Any<Guid>(), _workspaceId).Returns(new Workspace());

        _ai.GenerateJsonAsync(Arg.Any<AiJsonRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var prompt = JsonDocument.Parse(call.Arg<AiJsonRequest>().UserPrompt);
            var id = prompt.RootElement.GetProperty("features")[0].GetProperty("id").GetString();
            return new AiJsonResponse
            {
                Model = "fake",
                Json = $"{{\"scores\":[{{\"feature_id\":\"{id}\",\"complexity\":{_aiScore},\"reason\":\"r\"}}]}}"
            };
        });
    }

    private static SnapshotFeature F(string name, int complexity, FeatureOrigin origin = FeatureOrigin.FromAi) => new()
    {
        FeatureId = Guid.NewGuid(),
        Name = name,
        Complexity = complexity,
        Origin = origin,
        Status = SnapshotFeatureStatus.Locked
    };

    private static WorkspaceMember Member(Guid userId, WorkspaceMemberRole role, int? skill) => new()
    {
        UserId = userId,
        Role = role,
        Status = WorkspaceMemberStatus.Active,
        SkillLevel = skill
    };

    private void GivenMembers(params WorkspaceMember[] members)
    {
        _members.GetByWorkspaceIdAsync(_workspaceId).Returns(members.ToList());
        foreach (var member in members)
        {
            _members.GetByWorkspaceAndUserIdAsync(_workspaceId, member.UserId).Returns(member);
        }
    }

    private FeatureChallenge OpenChallenge(int? complexity = 2)
    {
        var challenge = new FeatureChallenge
        {
            WorkspaceId = _workspaceId,
            RequestedByUserId = _developer,
            Name = "new feature",
            Description = "d",
            Reason = "r",
            ProposedComplexity = 2,
            Complexity = complexity
        };
        _challenges.GetByIdAsync(_workspaceId, challenge.Id).Returns(challenge);
        return challenge;
    }

    private async Task<MvpSnapshot> Approve(FeatureChallenge challenge, ApproveChallengeRequest request)
    {
        MvpSnapshot? saved = null;
        await _definitions.AddSnapshotAsync(Arg.Do<MvpSnapshot>(s => saved = s));

        await _service.ApproveAsync(_manager, _workspaceId, challenge.Id, request);

        return saved!;
    }

    // ---- submit ----

    [Fact]
    public async Task Submit_RecordsTheRequestWithTheAiProposalAndNoConfirmedComplexity()
    {
        _aiScore = 4;

        await _service.SubmitAsync(_developer, _workspaceId,
            new SubmitChallengeRequest { Name = " Export ", Description = "d", Reason = "customer asked" });

        await _challenges.Received(1).AddAsync(Arg.Is<FeatureChallenge>(c =>
            c.Name == "Export"
            && c.RequestedByUserId == _developer
            && c.ProposedComplexity == 4
            && c.Complexity == null
            && c.Status == ChallengeStatus.Open));
        await _challenges.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Submit_AiScoreOutOfRange_IsRejectedAndNothingStored()
    {
        _aiScore = 9;

        await Assert.ThrowsAsync<AiOutputInvalidException>(() => _service.SubmitAsync(_developer, _workspaceId,
            new SubmitChallengeRequest { Name = "n", Description = "d", Reason = "r" }));

        await _challenges.DidNotReceive().AddAsync(Arg.Any<FeatureChallenge>());
    }

    [Fact]
    public async Task Submit_BeforeTheFirstLock_IsNotFound()
    {
        _definitions.GetLatestSnapshotAsync(_workspaceId).Returns((MvpSnapshot?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.SubmitAsync(_developer, _workspaceId,
            new SubmitChallengeRequest { Name = "n", Description = "d", Reason = "r" }));
    }

    // ---- confirm complexity ----

    [Fact]
    public async Task ConfirmComplexity_RequesterSetsIt()
    {
        var challenge = OpenChallenge(complexity: null);

        await _service.ConfirmComplexityAsync(_developer, _workspaceId, challenge.Id,
            new ConfirmChallengeComplexityRequest { Complexity = 4 });

        Assert.Equal(4, challenge.Complexity);
        Assert.Equal(2, challenge.ProposedComplexity);
    }

    [Fact]
    public async Task ConfirmComplexity_ManagerMayAdjustSomeoneElsesRequest()
    {
        var challenge = OpenChallenge(complexity: null);

        await _service.ConfirmComplexityAsync(_manager, _workspaceId, challenge.Id,
            new ConfirmChallengeComplexityRequest { Complexity = 1 });

        Assert.Equal(1, challenge.Complexity);
    }

    [Fact]
    public async Task ConfirmComplexity_AnotherDeveloper_IsRejected()
    {
        var challenge = OpenChallenge(complexity: null);
        var stranger = Guid.NewGuid();
        GivenMembers(Member(_manager, WorkspaceMemberRole.Manager, 5), Member(stranger, WorkspaceMemberRole.Developer, 3));

        await Assert.ThrowsAsync<WorkspacePermissionDeniedException>(() => _service.ConfirmComplexityAsync(
            stranger, _workspaceId, challenge.Id, new ConfirmChallengeComplexityRequest { Complexity = 4 }));
    }

    [Fact]
    public async Task ConfirmComplexity_AfterTheDecision_IsRejected()
    {
        var challenge = OpenChallenge();
        challenge.Status = ChallengeStatus.Rejected;

        await Assert.ThrowsAsync<ChallengeAlreadyDecidedException>(() => _service.ConfirmComplexityAsync(
            _manager, _workspaceId, challenge.Id, new ConfirmChallengeComplexityRequest { Complexity = 4 }));
    }

    // ---- approve: preconditions ----

    [Fact]
    public async Task Approve_NonManager_IsRejected()
    {
        var challenge = OpenChallenge();

        await Assert.ThrowsAsync<WorkspacePermissionDeniedException>(() => _service.ApproveAsync(
            _developer, _workspaceId, challenge.Id, new ApproveChallengeRequest { Action = ChallengeAction.AddDirectly }));
    }

    [Fact]
    public async Task Approve_ComplexityNotConfirmed_IsRejected()
    {
        var challenge = OpenChallenge(complexity: null);

        await Assert.ThrowsAsync<ChallengeComplexityNotConfirmedException>(() => _service.ApproveAsync(
            _manager, _workspaceId, challenge.Id, new ApproveChallengeRequest { Action = ChallengeAction.AddDirectly }));
    }

    [Fact]
    public async Task Approve_AlreadyDecided_IsRejected()
    {
        var challenge = OpenChallenge();
        challenge.Status = ChallengeStatus.Approved;

        await Assert.ThrowsAsync<ChallengeAlreadyDecidedException>(() => _service.ApproveAsync(
            _manager, _workspaceId, challenge.Id, new ApproveChallengeRequest { Action = ChallengeAction.AddDirectly }));
    }

    // ---- approve: add directly ----

    [Fact]
    public async Task Approve_AddDirectly_CreatesTheNextVersionAndRecordsTheDecision()
    {
        var challenge = OpenChallenge(complexity: 3);

        var next = await Approve(challenge, new ApproveChallengeRequest { Action = ChallengeAction.AddDirectly });

        Assert.Equal(2, next.Version);
        Assert.Equal(4, next.Features.Count);
        var added = next.Features.Single(f => f.FeatureId == challenge.FeatureId);
        Assert.Equal(SnapshotFeatureStatus.Locked, added.Status);
        Assert.Equal(FeatureOrigin.FromAi, added.Origin);
        Assert.Equal(64, next.Hash.Length);

        Assert.Equal(ChallengeStatus.Approved, challenge.Status);
        Assert.Equal(_manager, challenge.DecidedByUserId);
        Assert.Equal(ChallengeAction.AddDirectly, challenge.Action);
        Assert.Equal(2, challenge.ResultingSnapshotVersion);
        await _challenges.Received().SaveChangesAsync();
    }

    [Fact]
    public async Task Approve_AddDirectly_LeavesThePreviousVersionUntouched()
    {
        var challenge = OpenChallenge(complexity: 3);

        await Approve(challenge, new ApproveChallengeRequest { Action = ChallengeAction.AddDirectly });

        Assert.Equal(3, _snapshot.Features.Count);
        Assert.Equal(1, _snapshot.Version);
    }

    [Fact]
    public async Task Approve_AddDirectly_OverTheBudget_IsBlockedByTheGate()
    {
        var challenge = OpenChallenge(complexity: 5);

        var ex = await Assert.ThrowsAsync<LockGateFailedException>(() => _service.ApproveAsync(
            _manager, _workspaceId, challenge.Id, new ApproveChallengeRequest { Action = ChallengeAction.AddDirectly }));

        Assert.True(ex.Gate.BudgetExceeded);
        await _definitions.DidNotReceive().AddSnapshotAsync(Arg.Any<MvpSnapshot>());
        Assert.Equal(ChallengeStatus.Open, challenge.Status);
    }

    [Fact]
    public async Task Approve_AddDirectly_HarderThanTheBestDeveloper_IsBlockedByTheGate()
    {
        _b.Complexity = 2;
        GivenMembers(Member(_manager, WorkspaceMemberRole.Manager, skill: 2));
        var challenge = OpenChallenge(complexity: 3);

        var ex = await Assert.ThrowsAsync<LockGateFailedException>(() => _service.ApproveAsync(
            _manager, _workspaceId, challenge.Id, new ApproveChallengeRequest { Action = ChallengeAction.AddDirectly }));

        Assert.Single(ex.Gate.SkillCeilingViolations);
    }

    // ---- approve: backup ----

    [Fact]
    public async Task Approve_Backup_AddsTheFeatureAsBackupWhenNobodyCanTakeIt()
    {
        GivenMembers(Member(_manager, WorkspaceMemberRole.Manager, skill: 3), Member(_developer, WorkspaceMemberRole.Developer, 2));
        var challenge = OpenChallenge(complexity: 5);

        var next = await Approve(challenge, new ApproveChallengeRequest { Action = ChallengeAction.Backup });

        Assert.Equal(SnapshotFeatureStatus.Backup, next.Features.Single(f => f.FeatureId == challenge.FeatureId).Status);
    }

    [Fact]
    public async Task Approve_Backup_ForAFeatureSomeoneCanTake_IsRejected()
    {
        var challenge = OpenChallenge(complexity: 3);

        await Assert.ThrowsAsync<BackupNotApplicableException>(() => _service.ApproveAsync(
            _manager, _workspaceId, challenge.Id, new ApproveChallengeRequest { Action = ChallengeAction.Backup }));
    }

    // ---- approve: remove and replace ----

    [Fact]
    public async Task Approve_RemoveAndReplace_SwapsTheFeatureAndRecordsWhatWasRemoved()
    {
        var challenge = OpenChallenge(complexity: 4);

        var next = await Approve(challenge, new ApproveChallengeRequest
        {
            Action = ChallengeAction.RemoveAndReplace,
            RemoveFeatureId = _a.FeatureId
        });

        Assert.Equal(SnapshotFeatureStatus.Replaced, next.Features.Single(f => f.FeatureId == _a.FeatureId).Status);
        Assert.Equal(SnapshotFeatureStatus.Locked, next.Features.Single(f => f.FeatureId == challenge.FeatureId).Status);
        Assert.Equal(_a.FeatureId, challenge.RemovedFeatureId);
        Assert.Equal(SnapshotFeatureStatus.Locked, _a.Status);
    }

    [Fact]
    public async Task Approve_RemoveAndReplace_CommittedFeatureNeedsExplicitConfirmation()
    {
        var challenge = OpenChallenge(complexity: 2);
        var request = new ApproveChallengeRequest { Action = ChallengeAction.RemoveAndReplace, RemoveFeatureId = _committed.FeatureId };

        await Assert.ThrowsAsync<CommittedRemovalNotConfirmedException>(() =>
            _service.ApproveAsync(_manager, _workspaceId, challenge.Id, request));

        request.ConfirmCommittedRemoval = true;
        await _service.ApproveAsync(_manager, _workspaceId, challenge.Id, request);

        Assert.True(challenge.CommittedRemovalConfirmed);
    }

    [Fact]
    public async Task Approve_RemoveAndReplace_WithDependents_ListsThemUntilTheManagerChooses()
    {
        _snapshot.Dependencies.Add(new SnapshotDependency { FeatureId = _b.FeatureId, DependsOnFeatureId = _a.FeatureId });
        var challenge = OpenChallenge(complexity: 4);
        var request = new ApproveChallengeRequest { Action = ChallengeAction.RemoveAndReplace, RemoveFeatureId = _a.FeatureId };

        var ex = await Assert.ThrowsAsync<DependentsResolutionRequiredException>(() =>
            _service.ApproveAsync(_manager, _workspaceId, challenge.Id, request));
        Assert.Equal(_b.FeatureId, Assert.Single(ex.AffectedDependents).Id);

        request.DependentsResolution = RemovalDependentsResolution.RemoveDependents;
        var next = await Approve(challenge, request);

        Assert.Equal(SnapshotFeatureStatus.Replaced, next.Features.Single(f => f.FeatureId == _b.FeatureId).Status);
        Assert.Equal(RemovalDependentsResolution.RemoveDependents, challenge.DependentsResolution);
    }

    [Fact]
    public async Task Approve_RemoveAndReplace_UnknownFeature_IsNotFound()
    {
        var challenge = OpenChallenge(complexity: 2);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.ApproveAsync(_manager, _workspaceId, challenge.Id,
            new ApproveChallengeRequest { Action = ChallengeAction.RemoveAndReplace, RemoveFeatureId = Guid.NewGuid() }));
    }

    // ---- reject ----

    [Fact]
    public async Task Reject_EndsTheChallengeAndChangesNothingInTheSnapshot()
    {
        var challenge = OpenChallenge();

        await _service.RejectAsync(_manager, _workspaceId, challenge.Id, new RejectChallengeRequest { Reason = "  out of scope " });

        Assert.Equal(ChallengeStatus.Rejected, challenge.Status);
        Assert.Equal("out of scope", challenge.RejectionReason);
        Assert.Equal(_manager, challenge.DecidedByUserId);
        await _definitions.DidNotReceive().AddSnapshotAsync(Arg.Any<MvpSnapshot>());
    }

    [Fact]
    public async Task Reject_AlreadyDecided_IsRejected()
    {
        var challenge = OpenChallenge();
        challenge.Status = ChallengeStatus.Approved;

        await Assert.ThrowsAsync<ChallengeAlreadyDecidedException>(() =>
            _service.RejectAsync(_manager, _workspaceId, challenge.Id, new RejectChallengeRequest()));
    }

    [Fact]
    public async Task Reject_NonManager_IsRejected()
    {
        var challenge = OpenChallenge();

        await Assert.ThrowsAsync<WorkspacePermissionDeniedException>(() =>
            _service.RejectAsync(_developer, _workspaceId, challenge.Id, new RejectChallengeRequest()));
    }

    // ---- read ----

    [Fact]
    public async Task Get_OpenChallengeWithComplexity_ComesWithARecommendation()
    {
        var challenge = OpenChallenge(complexity: 3);

        var dto = await _service.GetAsync(_manager, _workspaceId, challenge.Id);

        Assert.Equal(ChallengeAction.AddDirectly, dto.Recommendation!.Action);
    }

    [Fact]
    public async Task Get_ComplexityNotConfirmedYet_HasNoRecommendation()
    {
        var challenge = OpenChallenge(complexity: null);

        var dto = await _service.GetAsync(_manager, _workspaceId, challenge.Id);

        Assert.Null(dto.Recommendation);
    }

    [Fact]
    public async Task Get_WhenTheBudgetIsFull_RecommendsReplacingAndNamesTheCandidateAndWhoDependsOnIt()
    {
        _b.Complexity = 7;
        _snapshot.Dependencies.Add(new SnapshotDependency { FeatureId = _committed.FeatureId, DependsOnFeatureId = _b.FeatureId });
        var challenge = OpenChallenge(complexity: 4);

        var dto = await _service.GetAsync(_manager, _workspaceId, challenge.Id);

        Assert.Equal(ChallengeAction.RemoveAndReplace, dto.Recommendation!.Action);
        Assert.Equal(_b.FeatureId, dto.Recommendation.FeatureToRemove!.Id);
        Assert.Equal(_committed.FeatureId, Assert.Single(dto.Recommendation.AffectedDependents).Id);
    }

    [Fact]
    public async Task Get_UnknownChallenge_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(_manager, _workspaceId, Guid.NewGuid()));
    }
}
