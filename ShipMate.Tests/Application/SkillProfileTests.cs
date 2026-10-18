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
    private readonly IWorkspaceAccessGuard _guard = Substitute.For<IWorkspaceAccessGuard>();
    private readonly WorkspaceService _service;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _workspaceId = Guid.NewGuid();

    public SkillProfileTests()
    {
        _service = new WorkspaceService(
            _workspaces, _members, _guard,
            Substitute.For<IProductDefinitionRepository>(),
            Substitute.For<IUserRepository>(),
            Substitute.For<IGitHubConnectionRepository>(),
            Substitute.For<IGitHubOAuthService>(),
            Substitute.For<IEncryptionService>(),
            Substitute.For<IEmailSender>(),
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
}
