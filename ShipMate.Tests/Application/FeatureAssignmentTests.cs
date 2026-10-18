using NSubstitute;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;
using ShipMate.Domain.Policies;

namespace ShipMate.Tests.Application;

public class FeatureAssignmentTests
{
    private readonly IProductDefinitionRepository _definitions = Substitute.For<IProductDefinitionRepository>();
    private readonly IFeatureAssignmentRepository _assignments = Substitute.For<IFeatureAssignmentRepository>();
    private readonly IWorkspaceMemberRepository _members = Substitute.For<IWorkspaceMemberRepository>();
    private readonly IWorkspaceAccessGuard _guard = Substitute.For<IWorkspaceAccessGuard>();
    private readonly FeatureAssignmentService _service;
    private readonly Guid _workspaceId = Guid.NewGuid();
    private readonly WorkspaceMember _me;
    private readonly SnapshotFeature _easy = SnapshotFeatureOf("easy", complexity: 2);
    private readonly SnapshotFeature _hard = SnapshotFeatureOf("hard", complexity: 5);

    public FeatureAssignmentTests()
    {
        _service = new FeatureAssignmentService(_definitions, _assignments, _members, _guard);

        _me = Member("Me", skill: 3, github: "me-dev");
        _members.GetByWorkspaceIdAsync(_workspaceId).Returns([_me]);
        _assignments.GetByWorkspaceIdAsync(_workspaceId).Returns([]);
        _assignments.TryAddAsync(Arg.Any<FeatureAssignment>()).Returns(true);

        var snapshot = new MvpSnapshot { WorkspaceId = _workspaceId, Version = 1 };
        snapshot.Features.Add(_easy);
        snapshot.Features.Add(_hard);
        _definitions.GetLatestSnapshotAsync(_workspaceId).Returns(snapshot);
    }

    private static SnapshotFeature SnapshotFeatureOf(string name, int complexity) =>
        new() { FeatureId = Guid.NewGuid(), Name = name, Complexity = complexity, Status = SnapshotFeatureStatus.Locked };

    private static WorkspaceMember Member(string name, int? skill, string? github) => new()
    {
        UserId = Guid.NewGuid(),
        Status = WorkspaceMemberStatus.Active,
        SkillLevel = skill,
        User = new User { DisplayName = name, GitHubUsername = github }
    };

    private Task<ShipMate.Application.DTOs.ProductDefinitions.FeatureAssignmentDto> Claim(SnapshotFeature feature) =>
        _service.ClaimAsync(_me.UserId, _workspaceId, feature.FeatureId);

    // ---- policy ----

    [Theory]
    [InlineData(3, 3, true)]
    [InlineData(4, 3, true)]
    [InlineData(2, 3, false)]
    [InlineData(null, 1, false)]
    public void IsQualified_NeedsSkillAtLeastTheComplexity(int? skill, int complexity, bool expected) =>
        Assert.Equal(expected, FeatureAssignmentPolicy.IsQualified(skill, complexity));

    [Fact]
    public void BranchName_FollowsTheConvention()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");

        Assert.Equal("feature/11111111-1111-1111-1111-111111111111-octocat", FeatureAssignmentPolicy.BranchNameFor(id, "octocat"));
    }

    // ---- claim ----

    [Fact]
    public async Task Claim_QualifiedDeveloper_GetsTheFeatureAndTheBranchName()
    {
        var result = await Claim(_easy);

        Assert.Equal(_me.UserId, result.AssignedUserId);
        Assert.Equal($"feature/{_easy.FeatureId}-me-dev", result.BranchName);
        await _assignments.Received(1).TryAddAsync(Arg.Is<FeatureAssignment>(a =>
            a.WorkspaceId == _workspaceId && a.FeatureId == _easy.FeatureId && a.AssignedUserId == _me.UserId));
    }

    [Fact]
    public async Task Claim_SkillBelowComplexity_IsRejectedAndFeatureStaysFree()
    {
        await Assert.ThrowsAsync<SkillTooLowForFeatureException>(() => Claim(_hard));

        await _assignments.DidNotReceive().TryAddAsync(Arg.Any<FeatureAssignment>());
    }

    [Fact]
    public async Task Claim_SkillNotDeclared_IsRejected()
    {
        _me.SkillLevel = null;

        await Assert.ThrowsAsync<SkillNotDeclaredException>(() => Claim(_easy));
    }

    [Fact]
    public async Task Claim_WithoutGitHubUsername_IsRejected()
    {
        _me.User.GitHubUsername = null;

        await Assert.ThrowsAsync<GitHubNotConnectedException>(() => Claim(_easy));
        await _assignments.DidNotReceive().TryAddAsync(Arg.Any<FeatureAssignment>());
    }

    [Fact]
    public async Task Claim_SomeoneElseWasFaster_IsRejected()
    {
        _assignments.TryAddAsync(Arg.Any<FeatureAssignment>()).Returns(false);

        await Assert.ThrowsAsync<FeatureAlreadyAssignedException>(() => Claim(_easy));
    }

    [Fact]
    public async Task Claim_FeatureNotInTheSnapshot_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ClaimAsync(_me.UserId, _workspaceId, Guid.NewGuid()));
    }

    [Fact]
    public async Task Claim_NeverLocked_IsNotFound()
    {
        _definitions.GetLatestSnapshotAsync(_workspaceId).Returns((MvpSnapshot?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => Claim(_easy));
    }

    [Fact]
    public async Task Claim_InvitedButNotActive_IsRejected()
    {
        _me.Status = WorkspaceMemberStatus.Invited;

        await Assert.ThrowsAsync<NotFoundException>(() => Claim(_easy));
    }

    // ---- backup and replaced features ----

    [Fact]
    public async Task Claim_BackupFeature_BecomesLockedInTheSameVersionAndTheHashFollows()
    {
        var backup = SnapshotFeatureOf("backup", complexity: 2);
        backup.Status = SnapshotFeatureStatus.Backup;
        var snapshot = await _definitions.GetLatestSnapshotAsync(_workspaceId);
        snapshot!.Features.Add(backup);
        snapshot.Hash = "old";

        await Claim(backup);

        Assert.Equal(SnapshotFeatureStatus.Locked, backup.Status);
        Assert.Equal(1, snapshot.Version);
        Assert.NotEqual("old", snapshot.Hash);
        Assert.Equal(64, snapshot.Hash.Length);
    }

    [Fact]
    public async Task Claim_BackupFeatureLostToSomeoneElse_StaysBackup()
    {
        var backup = SnapshotFeatureOf("backup", complexity: 2);
        backup.Status = SnapshotFeatureStatus.Backup;
        (await _definitions.GetLatestSnapshotAsync(_workspaceId))!.Features.Add(backup);
        _assignments.TryAddAsync(Arg.Any<FeatureAssignment>()).Returns(false);

        await Assert.ThrowsAsync<FeatureAlreadyAssignedException>(() => Claim(backup));

        Assert.Equal(SnapshotFeatureStatus.Backup, backup.Status);
    }

    [Fact]
    public async Task Claim_ReplacedFeature_IsNotFound()
    {
        var replaced = SnapshotFeatureOf("replaced", complexity: 1);
        replaced.Status = SnapshotFeatureStatus.Replaced;
        (await _definitions.GetLatestSnapshotAsync(_workspaceId))!.Features.Add(replaced);

        await Assert.ThrowsAsync<NotFoundException>(() => Claim(replaced));
    }

    [Fact]
    public async Task Overview_ListsBackupFeaturesButNotReplacedOnes()
    {
        var backup = SnapshotFeatureOf("backup", complexity: 2);
        backup.Status = SnapshotFeatureStatus.Backup;
        var replaced = SnapshotFeatureOf("replaced", complexity: 1);
        replaced.Status = SnapshotFeatureStatus.Replaced;
        var snapshot = (await _definitions.GetLatestSnapshotAsync(_workspaceId))!;
        snapshot.Features.Add(backup);
        snapshot.Features.Add(replaced);

        var overview = await _service.GetOverviewAsync(_me.UserId, _workspaceId);

        Assert.Contains(overview, o => o.FeatureId == backup.FeatureId && o.Status == SnapshotFeatureStatus.Backup);
        Assert.DoesNotContain(overview, o => o.FeatureId == replaced.FeatureId);
    }

    // ---- overview ----

    [Fact]
    public async Task Overview_ListsOnlyQualifiedMembersPerFeature()
    {
        var expert = Member("Expert", skill: 5, github: "expert");
        _members.GetByWorkspaceIdAsync(_workspaceId).Returns([_me, expert]);

        var overview = await _service.GetOverviewAsync(_me.UserId, _workspaceId);

        Assert.Equal(2, overview.Single(o => o.FeatureId == _easy.FeatureId).QualifiedMembers.Count);
        var hard = overview.Single(o => o.FeatureId == _hard.FeatureId);
        Assert.Equal(expert.UserId, Assert.Single(hard.QualifiedMembers).UserId);
    }

    [Fact]
    public async Task Overview_ShowsWhoTookAFeature()
    {
        _assignments.GetByWorkspaceIdAsync(_workspaceId).Returns(
        [
            new FeatureAssignment
            {
                FeatureId = _easy.FeatureId,
                AssignedUserId = _me.UserId,
                AssignedUser = _me.User,
                BranchName = "feature/x"
            }
        ]);

        var overview = await _service.GetOverviewAsync(_me.UserId, _workspaceId);

        Assert.Equal("Me", overview.Single(o => o.FeatureId == _easy.FeatureId).Assignee!.DisplayName);
        Assert.Null(overview.Single(o => o.FeatureId == _hard.FeatureId).Assignee);
    }
}
