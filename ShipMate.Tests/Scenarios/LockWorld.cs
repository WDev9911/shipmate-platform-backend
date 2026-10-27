using System.Text.Json;
using AutoMapper;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ShipMate.Application.DTOs.Ai;
using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests.Scenarios;

/// <summary>
/// A tiny in-memory workspace for scenario tests: the real services run against fake repositories that keep
/// state, so one test can walk through several steps (score, lock, assign, challenge) like a real session.
/// The AI is faked too, so no token is ever spent.
/// </summary>
internal sealed class LockWorld
{
    public Workspace Workspace { get; } = new() { Name = "Phòng khám", VisionPrompt = "Ứng dụng đặt lịch khám" };
    public ProductDefinition Definition { get; }
    public Guid WorkspaceId => Workspace.Id;

    public FakeProductDefinitionRepository Definitions { get; }
    public FakeMemberRepository Members { get; } = new();
    public FakeAssignmentRepository Assignments { get; }
    public FakeChallengeRepository Challenges { get; } = new();
    public FakeAiGateway Ai { get; } = new();
    public IWorkspaceNotifier Notifier { get; } = Substitute.For<IWorkspaceNotifier>();
    public InMemoryAuditTrailRepository AuditEntries { get; } = new();
    public AuditTrail Audit { get; }

    public LockService Lock { get; }
    public FeatureComplexityService Complexity { get; }
    public FeatureAssignmentService Assignment { get; }
    public FeatureChallengeService Challenge { get; }
    public ProductDefinitionService DefinitionService { get; }
    public FeatureReviewService Review { get; }
    public ProductDefinitionAnalysisService Analysis { get; }
    public WorkspaceService Workspaces { get; }

    public WorkspaceMember Manager { get; private set; } = null!;

    public LockWorld()
    {
        Assignments = new FakeAssignmentRepository(Members);
        Audit = new AuditTrail(AuditEntries);
        Definition = new ProductDefinition
        {
            WorkspaceId = Workspace.Id,
            Problem = "Bệnh nhân khó đặt lịch khám",
            Solution = "Đặt lịch khám online",
            LockedPersona = new ShipMate.Domain.ValueObjects.LockedPersona { PrimaryPersona = "Bệnh nhân", Reason = "Người đặt lịch" },
            Status = ProductDefinitionStatus.ReadyForLock
        };
        Definitions = new FakeProductDefinitionRepository(Definition);

        var guard = new FakeGuard(Workspace, Members);
        var mapper = Substitute.For<IMapper>();
        mapper.Map<FeatureChallengeDto>(Arg.Any<object>()).Returns(call =>
        {
            var c = (FeatureChallenge)call.Args()[0];
            return new FeatureChallengeDto
            {
                Id = c.Id, FeatureId = c.FeatureId, Name = c.Name, Status = c.Status,
                ProposedComplexity = c.ProposedComplexity, Complexity = c.Complexity,
                Action = c.Action, ResultingSnapshotVersion = c.ResultingSnapshotVersion,
                RejectionReason = c.RejectionReason
            };
        });

        Lock = new LockService(Definitions, Members, guard, Audit, mapper);
        Complexity = new FeatureComplexityService(Definitions, guard, Ai, mapper);
        Assignment = new FeatureAssignmentService(Definitions, Assignments, Members, guard,
            Substitute.For<IGitHubAppService>(), Notifier);
        Challenge = new FeatureChallengeService(Challenges, Definitions, Assignments, Members, guard, Ai, Audit, Notifier, mapper);
        DefinitionService = new ProductDefinitionService(Definitions, guard, mapper);
        Review = new FeatureReviewService(Definitions, guard, mapper);
        Analysis = new ProductDefinitionAnalysisService(
            Definitions, guard, Ai, mapper, Substitute.For<ILogger<ProductDefinitionAnalysisService>>());
        Workspaces = new WorkspaceService(
            Substitute.For<IWorkspaceRepository>(), Members, guard, Definitions,
            Substitute.For<IUserRepository>(), Substitute.For<IGitHubConnectionRepository>(),
            Substitute.For<IGitHubOAuthService>(), Substitute.For<IGitHubAppService>(), Substitute.For<IEncryptionService>(),
            Substitute.For<IEmailSender>(), Notifier, mapper);
    }

    // ---- building the scene ----

    public WorkspaceMember AddMember(string name, WorkspaceMemberRole role, int? skill, string? github = "")
    {
        var member = new WorkspaceMember
        {
            WorkspaceId = WorkspaceId,
            Role = role,
            Status = WorkspaceMemberStatus.Active,
            SkillLevel = skill,
            User = new User { DisplayName = name, GitHubUsername = github == "" ? name.ToLowerInvariant() : github }
        };
        member.UserId = member.User.Id;
        Members.Add(member);
        if (role == WorkspaceMemberRole.Manager)
        {
            Manager = member;
        }

        return member;
    }

    /// <summary>A feature of the product definition, already included. Pass null complexity to leave it unscored.</summary>
    public Feature AddFeature(
        string name, int? complexity, FeatureOrigin origin = FeatureOrigin.FromAi, params Feature[] dependsOn)
    {
        var feature = new Feature
        {
            ProductDefinitionId = Definition.Id,
            Name = name,
            Description = $"{name} description",
            Origin = origin,
            Sources = [origin],
            Status = FeatureStatus.Included,
            Role = FeatureRole.Primary,
            AiVerdict = AiVerdict.Required,
            Complexity = complexity,
            Position = Definition.Features.Count
        };
        foreach (var prerequisite in dependsOn)
        {
            feature.Dependencies.Add(new FeatureDependency
            {
                FeatureId = feature.Id,
                DependsOnFeatureId = prerequisite.Id,
                DependsOnFeature = prerequisite
            });
        }

        Definition.Features.Add(feature);
        return feature;
    }

    // ---- shortcuts used by many scenarios ----

    public Guid ManagerId => Manager.UserId;

    public Task<MvpSnapshotDto> LockNow() => Lock.LockAsync(ManagerId, WorkspaceId);

    public MvpSnapshot LatestSnapshot => Definitions.Snapshots.OrderByDescending(s => s.Version).First();

    public SnapshotFeature InSnapshot(MvpSnapshot snapshot, string name) =>
        snapshot.Features.Single(f => f.Name == name);

    public async Task<FeatureChallengeDto> SubmitChallenge(
        WorkspaceMember by, string name, int aiScore, int? confirmedComplexity = null)
    {
        Ai.ScoreByName[name] = aiScore;
        var challenge = await Challenge.SubmitAsync(by.UserId, WorkspaceId,
            new SubmitChallengeRequest { Name = name, Description = $"{name} description", Reason = "customer request" });

        if (confirmedComplexity is not null)
        {
            await Challenge.ConfirmComplexityAsync(by.UserId, WorkspaceId, challenge.Id,
                new ConfirmChallengeComplexityRequest { Complexity = confirmedComplexity.Value });
        }

        return challenge;
    }
}

internal sealed class FakeGuard : IWorkspaceAccessGuard
{
    private readonly Workspace _workspace;
    private readonly FakeMemberRepository _members;

    public FakeGuard(Workspace workspace, FakeMemberRepository members)
    {
        _workspace = workspace;
        _members = members;
    }

    public Task<Workspace> GetWorkspaceAsMemberAsync(Guid userId, Guid workspaceId)
    {
        var member = _members.Find(userId);
        if (member is null || member.Status != WorkspaceMemberStatus.Active)
        {
            throw new NotFoundException("Workspace", workspaceId);
        }

        return Task.FromResult(_workspace);
    }

    public async Task<Workspace> GetWorkspaceAsManagerAsync(Guid userId, Guid workspaceId)
    {
        await GetWorkspaceAsMemberAsync(userId, workspaceId);
        if (_members.Find(userId)!.Role != WorkspaceMemberRole.Manager)
        {
            throw new WorkspacePermissionDeniedException();
        }

        return _workspace;
    }
}

internal sealed class FakeMemberRepository : IWorkspaceMemberRepository
{
    private readonly List<WorkspaceMember> _members = new();

    public void Add(WorkspaceMember member) => _members.Add(member);
    public WorkspaceMember? Find(Guid userId) => _members.FirstOrDefault(m => m.UserId == userId);

    public Task<WorkspaceMember?> GetByWorkspaceAndUserIdAsync(Guid workspaceId, Guid userId) =>
        Task.FromResult(Find(userId));

    public Task<List<WorkspaceMember>> GetByWorkspaceIdAsync(Guid workspaceId) =>
        Task.FromResult(_members.Where(m => m.Status != WorkspaceMemberStatus.Removed).ToList());

    public Task<List<WorkspaceMember>> GetPendingInvitationsByUserIdAsync(Guid userId) =>
        Task.FromResult(new List<WorkspaceMember>());

    public Task AddAsync(WorkspaceMember member)
    {
        _members.Add(member);
        return Task.CompletedTask;
    }

    public void Update(WorkspaceMember member)
    {
    }
}

internal sealed class FakeProductDefinitionRepository : IProductDefinitionRepository
{
    private readonly ProductDefinition _definition;

    public List<MvpSnapshot> Snapshots { get; } = new();
    public int Saves { get; private set; }

    public FakeProductDefinitionRepository(ProductDefinition definition)
    {
        _definition = definition;
    }

    public Task<ProductDefinition?> GetByWorkspaceIdAsync(Guid workspaceId) =>
        Task.FromResult<ProductDefinition?>(_definition);

    // The scenarios start from an analyzed definition.
    public Task<bool> HasSuccessfulAnalysisRunAsync(Guid productDefinitionId) => Task.FromResult(true);

    public Task AddAsync(ProductDefinition productDefinition) => Task.CompletedTask;

    public Task AddFeatureAsync(Feature feature)
    {
        _definition.Features.Add(feature);
        return Task.CompletedTask;
    }

    public void RemoveFeature(Feature feature) => _definition.Features.Remove(feature);
    public Task AddDependencyAsync(FeatureDependency dependency) => Task.CompletedTask;
    public void RemoveDependency(FeatureDependency dependency)
    {
    }

    public Task AddAnalysisRunAsync(AiAnalysisRun run) => Task.CompletedTask;
    public Task AddChangeLogAsync(FeatureChangeLog changeLog) => Task.CompletedTask;
    public Task<List<FeatureChangeLog>> GetChangeLogsAsync(Guid featureId) => Task.FromResult(new List<FeatureChangeLog>());

    public Task AddSnapshotAsync(MvpSnapshot snapshot)
    {
        Snapshots.Add(snapshot);
        return Task.CompletedTask;
    }

    public Task<MvpSnapshot?> GetLatestSnapshotAsync(Guid workspaceId) =>
        Task.FromResult(Snapshots.OrderByDescending(s => s.Version).FirstOrDefault());

    public Task<bool> SaveChangesAsync()
    {
        Saves++;
        return Task.FromResult(true);
    }
}

/// <summary>Behaves like the unique index on feature_id: the second claim on a feature loses.</summary>
internal sealed class FakeAssignmentRepository : IFeatureAssignmentRepository
{
    private readonly List<FeatureAssignment> _assignments = new();
    private readonly FakeMemberRepository _members;

    public FakeAssignmentRepository(FakeMemberRepository members)
    {
        _members = members;
    }

    public Task<List<FeatureAssignment>> GetByWorkspaceIdAsync(Guid workspaceId) =>
        Task.FromResult(_assignments.ToList());

    public Task<bool> TryAddAsync(FeatureAssignment assignment)
    {
        if (_assignments.Any(a => a.FeatureId == assignment.FeatureId))
        {
            return Task.FromResult(false);
        }

        // The real repository loads the user with the assignment.
        assignment.AssignedUser = _members.Find(assignment.AssignedUserId)!.User;
        _assignments.Add(assignment);
        return Task.FromResult(true);
    }

    public Task<bool> SaveChangesAsync() => Task.FromResult(true);
}

internal sealed class FakeChallengeRepository : IFeatureChallengeRepository
{
    private readonly List<FeatureChallenge> _challenges = new();

    public Task<FeatureChallenge?> GetByIdAsync(Guid workspaceId, Guid challengeId) =>
        Task.FromResult(_challenges.FirstOrDefault(c => c.Id == challengeId));

    public Task<List<FeatureChallenge>> GetByWorkspaceIdAsync(Guid workspaceId) =>
        Task.FromResult(_challenges.OrderByDescending(c => c.CreatedAt).ToList());

    public Task AddAsync(FeatureChallenge challenge)
    {
        _challenges.Add(challenge);
        return Task.CompletedTask;
    }

    public Task<bool> SaveChangesAsync() => Task.FromResult(true);
}

/// <summary>Scores each feature by its name (default 3), the way the real AI answers the scoring prompt.</summary>
internal sealed class FakeAiGateway : IAiGateway
{
    public string Model => "fake";
    public Dictionary<string, int> ScoreByName { get; } = new();
    public int Calls { get; private set; }

    // Answer to the persona conflict prompt; null makes the fake reply with an invalid (empty) reason.
    public (bool Conflict, string Reason)? PersonaAnswer { get; set; } = (false, "Phù hợp");

    public Task<AiJsonResponse> GenerateJsonAsync(AiJsonRequest request, CancellationToken cancellationToken = default)
    {
        Calls++;
        var root = JsonDocument.Parse(request.UserPrompt).RootElement;
        if (root.TryGetProperty("persona", out _))
        {
            var answer = PersonaAnswer ?? (false, "");
            return Task.FromResult(new AiJsonResponse
            {
                Model = "fake",
                Json = JsonSerializer.Serialize(new { persona_conflict = answer.Conflict, reason = answer.Reason })
            });
        }

        var features = JsonDocument.Parse(request.UserPrompt).RootElement.GetProperty("features");
        var scores = features.EnumerateArray().Select(f =>
        {
            var name = f.GetProperty("name").GetString()!;
            var score = ScoreByName.GetValueOrDefault(name, 3);
            return $"{{\"feature_id\":\"{f.GetProperty("id").GetString()}\",\"complexity\":{score},\"reason\":\"r\"}}";
        });

        return Task.FromResult(new AiJsonResponse { Model = "fake", Json = $"{{\"scores\":[{string.Join(",", scores)}]}}" });
    }
}
