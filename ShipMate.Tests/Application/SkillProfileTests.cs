using AutoMapper;
using NSubstitute;
using ShipMate.Application.DTOs.Workspaces;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Application.Validators.Workspaces;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests.Application;

public class SkillProfileTests
{
    private readonly IWorkspaceMemberRepository _members = Substitute.For<IWorkspaceMemberRepository>();
    private readonly IWorkspaceRepository _workspaces = Substitute.For<IWorkspaceRepository>();
    private readonly IProductDefinitionRepository _definitions = Substitute.For<IProductDefinitionRepository>();
    private readonly IWorkspaceNotifier _notifier = Substitute.For<IWorkspaceNotifier>();
    private readonly IWorkspaceAccessGuard _guard = Substitute.For<IWorkspaceAccessGuard>();
    private readonly WorkspaceService _service;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _workspaceId = Guid.NewGuid();

    public SkillProfileTests()
    {
        _service = new WorkspaceService(
            _workspaces, _members, _guard,
            _definitions,
            Substitute.For<IUserRepository>(),
            Substitute.For<IGitHubConnectionRepository>(),
            Substitute.For<IGitHubOAuthService>(),
            Substitute.For<IGitHubAppService>(),
            Substitute.For<IEncryptionService>(),
            Substitute.For<IEmailSender>(),
            _notifier,
            Substitute.For<IMapper>());
    }

    private WorkspaceMember GivenMember(WorkspaceMemberStatus status = WorkspaceMemberStatus.Active)
    {
        var member = new WorkspaceMember { WorkspaceId = _workspaceId, UserId = _userId, Status = status };
        _members.GetByWorkspaceAndUserIdAsync(_workspaceId, _userId).Returns(member);
        return member;
    }

    [Fact]
    public async Task NewMember_HasNoSkillYet()
    {
        Assert.Null(GivenMember().SkillLevel);
    }

    [Fact]
    public async Task Member_CanDeclareTheirOwnSkill()
    {
        var member = GivenMember();

        await _service.UpdateMySkillAsync(_userId, _workspaceId, new UpdateMySkillRequest { SkillLevel = 4 });

        Assert.Equal(4, member.SkillLevel);
        await _workspaces.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Member_CanChangeSkillAgain()
    {
        var member = GivenMember();
        member.SkillLevel = 2;

        await _service.UpdateMySkillAsync(_userId, _workspaceId, new UpdateMySkillRequest { SkillLevel = 5 });

        Assert.Equal(5, member.SkillLevel);
    }

    [Fact]
    public async Task InvitedButNotAccepted_CannotDeclareSkill()
    {
        GivenMember(WorkspaceMemberStatus.Invited);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateMySkillAsync(_userId, _workspaceId, new UpdateMySkillRequest { SkillLevel = 3 }));

        await _workspaces.DidNotReceive().SaveChangesAsync();
    }

    // ---- skill declared when joining (accepting the invitation) ----

    [Fact]
    public async Task AcceptInvitation_ActivatesTheMemberWithTheDeclaredSkill()
    {
        var member = GivenMember(WorkspaceMemberStatus.Invited);

        await _service.AcceptInvitationAsync(_userId, _workspaceId, new AcceptInvitationRequest { SkillLevel = 4 });

        Assert.Equal(WorkspaceMemberStatus.Active, member.Status);
        Assert.Equal(4, member.SkillLevel);
    }

    [Fact]
    public async Task AcceptInvitation_WithoutAPendingInvitation_IsNotFound()
    {
        GivenMember(WorkspaceMemberStatus.Active);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.AcceptInvitationAsync(_userId, _workspaceId, new AcceptInvitationRequest { SkillLevel = 3 }));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void AcceptInvitationValidator_RequiresASkillFromOneToFive(int level, bool valid) =>
        Assert.Equal(valid, new AcceptInvitationRequestValidator().Validate(new AcceptInvitationRequest { SkillLevel = level }).IsValid);

    [Theory]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(0, false)]
    [InlineData(6, false)]
    [InlineData(-1, false)]
    public void Validator_AcceptsOnlyOneToFive(int level, bool valid)
    {
        var result = new UpdateMySkillRequestValidator().Validate(new UpdateMySkillRequest { SkillLevel = level });

        Assert.Equal(valid, result.IsValid);
    }

    // ---- a backup feature that now fits ----

    private void GivenBackupFeature(int complexity, SnapshotFeatureStatus status = SnapshotFeatureStatus.Backup)
    {
        var snapshot = new MvpSnapshot { WorkspaceId = _workspaceId, Version = 1 };
        snapshot.Features.Add(new SnapshotFeature { FeatureId = Guid.NewGuid(), Name = "Offline sync", Complexity = complexity, Status = status });
        _definitions.GetLatestSnapshotAsync(_workspaceId).Returns(snapshot);
    }

    [Fact]
    public async Task DeclaringASkillThatReachesABackupFeature_TellsTheMember()
    {
        GivenMember();
        GivenBackupFeature(complexity: 4);

        await _service.UpdateMySkillAsync(_userId, _workspaceId, new UpdateMySkillRequest { SkillLevel = 4 });

        await _notifier.Received(1).PublishToUsersAsync(
            Arg.Is<IEnumerable<Guid>>(users => users.Single() == _userId), WorkspaceEvents.BackupFeatureMatched, Arg.Any<object>());
    }

    [Fact]
    public async Task ASkillBelowTheBackupFeature_TellsNobody()
    {
        GivenMember();
        GivenBackupFeature(complexity: 5);

        await _service.UpdateMySkillAsync(_userId, _workspaceId, new UpdateMySkillRequest { SkillLevel = 4 });

        await _notifier.DidNotReceiveWithAnyArgs().PublishToUsersAsync(default!, default!, default!);
    }

    [Fact]
    public async Task ALockedFeatureIsNotABackupMatch()
    {
        GivenMember();
        GivenBackupFeature(complexity: 2, status: SnapshotFeatureStatus.Locked);

        await _service.UpdateMySkillAsync(_userId, _workspaceId, new UpdateMySkillRequest { SkillLevel = 5 });

        await _notifier.DidNotReceiveWithAnyArgs().PublishToUsersAsync(default!, default!, default!);
    }

    [Fact]
    public async Task BeforeTheFirstLock_ThereIsNothingToMatch()
    {
        GivenMember();

        await _service.UpdateMySkillAsync(_userId, _workspaceId, new UpdateMySkillRequest { SkillLevel = 5 });

        await _notifier.DidNotReceiveWithAnyArgs().PublishToUsersAsync(default!, default!, default!);
    }

    [Fact]
    public async Task JoiningWithASkillThatReachesABackupFeature_TellsTheNewMember()
    {
        GivenMember(WorkspaceMemberStatus.Invited);
        GivenBackupFeature(complexity: 3);

        await _service.AcceptInvitationAsync(_userId, _workspaceId, new AcceptInvitationRequest { SkillLevel = 3 });

        await _notifier.Received(1).PublishToUsersAsync(
            Arg.Is<IEnumerable<Guid>>(users => users.Single() == _userId), WorkspaceEvents.BackupFeatureMatched, Arg.Any<object>());
    }
}
