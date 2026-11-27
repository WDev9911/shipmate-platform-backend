using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ShipMate.Application.DTOs.Workspaces;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Mappings;
using ShipMate.Application.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests.Application;

// An administrator account runs the platform; it never creates, joins or opens a workspace.
public class AdminWorkspaceBlockTests
{
    private readonly IWorkspaceRepository _workspaces = Substitute.For<IWorkspaceRepository>();
    private readonly IWorkspaceMemberRepository _members = Substitute.For<IWorkspaceMemberRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPlatformSettingsRepository _platformSettings = Substitute.For<IPlatformSettingsRepository>();
    private readonly IWorkspaceAccessGuard _guard = Substitute.For<IWorkspaceAccessGuard>();
    private readonly WorkspaceService _service;
    private readonly Workspace _workspace = new() { Name = "Phòng khám" };
    private readonly Guid _manager = Guid.NewGuid();

    public AdminWorkspaceBlockTests()
    {
        var mapper = Substitute.For<IMapper>();
        mapper.Map<WorkspaceDto>(Arg.Any<object>()).Returns(_ => new WorkspaceDto());
        mapper.Map<WorkspaceMemberDto>(Arg.Any<object>()).Returns(_ => new WorkspaceMemberDto());

        _service = new WorkspaceService(
            _workspaces, _members, _guard,
            Substitute.For<IProductDefinitionRepository>(), _users,
            Substitute.For<IGitHubConnectionRepository>(), Substitute.For<IGitHubOAuthService>(),
            Substitute.For<IGitHubAppService>(), Substitute.For<IEncryptionService>(), Substitute.For<IEmailSender>(),
            Substitute.For<IWorkspaceNotifier>(), Substitute.For<IAuditTrail>(),
            Substitute.For<IFeatureAssignmentRepository>(), _platformSettings, mapper);

        _guard.GetWorkspaceAsManagerAsync(_manager, _workspace.Id).Returns(_workspace);
        _platformSettings.GetAsync().Returns(new PlatformSettings());
    }

    private User Account(UserRole role = UserRole.Developer, UserStatus status = UserStatus.Active)
    {
        var user = new User { Email = $"{Guid.NewGuid():N}@example.com", Role = role, Status = status };
        _users.GetByIdAsync(user.Id).Returns(user);
        _users.GetByEmailAsync(user.Email).Returns(user);
        return user;
    }

    // ---------- Creating a workspace ----------

    [Fact]
    public async Task Create_AsAnAdministrator_IsForbidden_AndNothingIsSaved()
    {
        var admin = Account(UserRole.Administrator);

        var error = await Assert.ThrowsAsync<AdminCannotUseWorkspacesException>(() =>
            _service.CreateAsync(admin.Id, new CreateWorkspaceRequest { Name = "X", VisionPrompt = "Y" }));

        Assert.Equal(403, error.StatusCode);
        Assert.Equal("ADMIN_CANNOT_USE_WORKSPACES", error.ErrorCode);
        await _workspaces.DidNotReceive().AddAsync(Arg.Any<Workspace>());
        await _workspaces.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Create_AsADeveloper_StillWorks_AndMakesThemManager()
    {
        var developer = Account();

        await _service.CreateAsync(developer.Id, new CreateWorkspaceRequest { Name = "X", VisionPrompt = "Y" });

        await _workspaces.Received(1).AddAsync(Arg.Any<Workspace>());
        await _members.Received(1).AddAsync(Arg.Is<WorkspaceMember>(m =>
            m.UserId == developer.Id && m.Role == WorkspaceMemberRole.Manager));
    }

    // ---------- Inviting someone ----------

    [Theory]
    [InlineData(UserRole.Administrator, UserStatus.Active)]
    [InlineData(UserRole.Developer, UserStatus.Locked)]
    [InlineData(UserRole.Developer, UserStatus.Removed)]
    public async Task Invite_AnAdministratorOrAClosedAccount_LooksLikeAnUnknownEmail(UserRole role, UserStatus status)
    {
        var target = Account(role, status);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.AddMemberAsync(_manager, _workspace.Id, new AddMemberRequest { Email = target.Email }));

        await _members.DidNotReceive().AddAsync(Arg.Any<WorkspaceMember>());
        _members.DidNotReceive().Update(Arg.Any<WorkspaceMember>());
    }

    [Fact]
    public async Task Invite_AnActiveDeveloper_StillWorks()
    {
        var target = Account();

        await _service.AddMemberAsync(_manager, _workspace.Id, new AddMemberRequest { Email = target.Email });

        await _members.Received(1).AddAsync(Arg.Is<WorkspaceMember>(m =>
            m.UserId == target.Id && m.Status == WorkspaceMemberStatus.Invited));
    }

    // ---------- Opening a workspace (the guard every workspace API goes through) ----------

    private WorkspaceAccessGuard RealGuard(User user, WorkspaceMemberRole role, WorkspaceMemberStatus status)
    {
        _workspaces.GetByIdAsync(_workspace.Id).Returns(_workspace);
        _members.GetByWorkspaceAndUserIdAsync(_workspace.Id, user.Id).Returns(new WorkspaceMember
        {
            WorkspaceId = _workspace.Id, UserId = user.Id, User = user, Role = role, Status = status
        });
        return new WorkspaceAccessGuard(_workspaces, _members);
    }

    [Fact]
    public async Task Guard_AnAdministratorWhoIsAMemberAnyway_GetsNotFound_AsMemberAndAsManager()
    {
        var admin = Account(UserRole.Administrator);
        var guard = RealGuard(admin, WorkspaceMemberRole.Manager, WorkspaceMemberStatus.Active);

        await Assert.ThrowsAsync<NotFoundException>(() => guard.GetWorkspaceAsMemberAsync(admin.Id, _workspace.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => guard.GetWorkspaceAsManagerAsync(admin.Id, _workspace.Id));
    }

    [Fact]
    public async Task Guard_AnArchivedWorkspaceIsClosedToAnAdministratorToo()
    {
        var admin = Account(UserRole.Administrator);
        _workspace.Status = WorkspaceStatus.Archived;
        var guard = RealGuard(admin, WorkspaceMemberRole.Developer, WorkspaceMemberStatus.Active);

        await Assert.ThrowsAsync<NotFoundException>(() => guard.GetWorkspaceAsMemberAsync(admin.Id, _workspace.Id));
    }

    [Fact]
    public async Task Guard_ADeveloperMember_StillGetsIn()
    {
        var developer = Account();
        var guard = RealGuard(developer, WorkspaceMemberRole.Manager, WorkspaceMemberStatus.Active);

        Assert.Same(_workspace, await guard.GetWorkspaceAsMemberAsync(developer.Id, _workspace.Id));
        Assert.Same(_workspace, await guard.GetWorkspaceAsManagerAsync(developer.Id, _workspace.Id));
    }

    // ---------- The Manager sees who can no longer take part ----------

    [Theory]
    [InlineData(UserStatus.Active, false)]
    [InlineData(UserStatus.Locked, true)]
    [InlineData(UserStatus.Removed, true)]
    public void MemberList_MarksAccountsThatAreClosed(UserStatus status, bool locked)
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<WorkspaceProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var user = new User { Email = "m@example.com", DisplayName = "M", Status = status };
        var member = new WorkspaceMember { UserId = user.Id, User = user };

        Assert.Equal(locked, mapper.Map<WorkspaceMemberDto>(member).AccountLocked);
    }

    // ---------- A new workspace takes a copy of the platform settings ----------

    [Fact]
    public async Task Create_CopiesTheBudgetAndTheDriftThresholdFromTheSettings()
    {
        var developer = Account();
        _platformSettings.GetAsync().Returns(new PlatformSettings
        {
            ComplexityBudgetPerDeveloper = 15, DefaultDriftThreshold = 0.80m
        });
        Workspace? created = null;
        await _workspaces.AddAsync(Arg.Do<Workspace>(w => created = w));

        await _service.CreateAsync(developer.Id, new CreateWorkspaceRequest { Name = "X", VisionPrompt = "Y" });

        Assert.Equal(15, created!.ComplexityBudgetPerDeveloper);
        Assert.Equal(0.80m, created.DriftConfidenceThreshold);
    }

    [Fact]
    public async Task Create_AfterTheSettingsChange_OnlyTheNewWorkspaceGetsTheNewValues()
    {
        var developer = Account();
        var settings = new PlatformSettings { ComplexityBudgetPerDeveloper = 10, DefaultDriftThreshold = 0.75m };
        _platformSettings.GetAsync().Returns(settings);
        var created = new List<Workspace>();
        await _workspaces.AddAsync(Arg.Do<Workspace>(created.Add));
        var request = new CreateWorkspaceRequest { Name = "X", VisionPrompt = "Y" };

        await _service.CreateAsync(developer.Id, request);
        settings.ComplexityBudgetPerDeveloper = 25;
        settings.DefaultDriftThreshold = 0.70m;
        await _service.CreateAsync(developer.Id, request);

        Assert.Equal(10, created[0].ComplexityBudgetPerDeveloper);
        Assert.Equal(0.75m, created[0].DriftConfidenceThreshold);
        Assert.Equal(25, created[1].ComplexityBudgetPerDeveloper);
        Assert.Equal(0.70m, created[1].DriftConfidenceThreshold);
    }

    [Fact]
    public async Task Create_WithoutTheSettingsRow_FailsLoudly_InsteadOfInventingDefaults()
    {
        var developer = Account();
        _platformSettings.GetAsync().Returns<PlatformSettings>(_ => throw new InvalidOperationException("missing"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAsync(developer.Id, new CreateWorkspaceRequest { Name = "X", VisionPrompt = "Y" }));

        await _workspaces.DidNotReceive().AddAsync(Arg.Any<Workspace>());
    }

    [Fact]
    public void ANewWorkspaceObject_StartsWithTheOldTenPoints_SoExistingWorkspacesAndTestsAreUnchanged()
    {
        Assert.Equal(10, new Workspace().ComplexityBudgetPerDeveloper);
        Assert.Equal(0.75m, new Workspace().DriftConfidenceThreshold);
    }
}
