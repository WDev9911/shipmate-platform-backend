using AutoMapper;
using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;
using ShipMate.Domain.Policies;

namespace ShipMate.Application.Services;

public class LockService : ILockService
{
    private const int FirstVersion = 1;

    private readonly IProductDefinitionRepository _productDefinitionRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IWorkspaceAccessGuard _workspaceAccessGuard;
    private readonly IMapper _mapper;

    public LockService(
        IProductDefinitionRepository productDefinitionRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IWorkspaceAccessGuard workspaceAccessGuard,
        IMapper mapper)
    {
        _productDefinitionRepository = productDefinitionRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _workspaceAccessGuard = workspaceAccessGuard;
        _mapper = mapper;
    }

    public async Task<LockGateDto> CheckAsync(Guid userId, Guid workspaceId)
    {
        await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);

        var productDefinition = await GetDefinitionOrThrow(workspaceId);
        var (_, gate, _) = await EvaluateAsync(productDefinition);
        return LockGateDto.From(gate);
    }

    public async Task<MvpSnapshotDto> LockAsync(Guid userId, Guid workspaceId)
    {
        var workspace = await _workspaceAccessGuard.GetWorkspaceAsManagerAsync(userId, workspaceId);

        var productDefinition = await GetDefinitionOrThrow(workspaceId);
        if (productDefinition.IsLocked)
        {
            throw new ProductDefinitionLockedException();
        }

        if (productDefinition.Status != ProductDefinitionStatus.ReadyForLock)
        {
            throw new ProductDefinitionNotReadyForLockException();
        }

        var (included, gate, devCount) = await EvaluateAsync(productDefinition);
        if (!gate.IsPassed)
        {
            throw new LockGateFailedException(LockGateDto.From(gate));
        }

        var snapshot = new MvpSnapshot
        {
            WorkspaceId = workspaceId,
            Version = FirstVersion,
            DevCountAtLock = devCount,
            CreatedByUserId = userId
        };

        foreach (var feature in included)
        {
            snapshot.Features.Add(new SnapshotFeature
            {
                FeatureId = feature.Id,
                Name = feature.Name,
                Description = feature.Description,
                Scope = feature.Scope,
                Role = feature.Role,
                Origin = feature.Origin,
                Complexity = feature.Complexity!.Value,
                Status = SnapshotFeatureStatus.Locked
            });

            foreach (var dependency in feature.Dependencies)
            {
                snapshot.Dependencies.Add(new SnapshotDependency
                {
                    FeatureId = feature.Id,
                    DependsOnFeatureId = dependency.DependsOnFeatureId
                });
            }
        }

        snapshot.Hash = MvpSnapshotHasher.Compute(snapshot);
        await _productDefinitionRepository.AddSnapshotAsync(snapshot);

        var now = DateTime.UtcNow;
        productDefinition.MarkLocked(now);

        // SHIP measures Time-to-Ship from here, so it is written once and never moved.
        workspace.FirstLockedAt ??= now;

        await _productDefinitionRepository.SaveChangesAsync();

        return _mapper.Map<MvpSnapshotDto>(snapshot);
    }

    public async Task<MvpSnapshotDto> GetLatestSnapshotAsync(Guid userId, Guid workspaceId)
    {
        await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);

        var snapshot = await _productDefinitionRepository.GetLatestSnapshotAsync(workspaceId)
            ?? throw new NotFoundException("MVP snapshot", workspaceId);

        return _mapper.Map<MvpSnapshotDto>(snapshot);
    }

    private async Task<ProductDefinition> GetDefinitionOrThrow(Guid workspaceId)
    {
        var productDefinition = await _productDefinitionRepository.GetByWorkspaceIdAsync(workspaceId);
        if (productDefinition is null
            || !await _productDefinitionRepository.HasSuccessfulAnalysisRunAsync(productDefinition.Id))
        {
            throw new ProductDefinitionNotAnalyzedException();
        }

        return productDefinition;
    }

    private async Task<(List<Feature> Included, LockGateResult Gate, int DevCount)> EvaluateAsync(ProductDefinition productDefinition)
    {
        var included = productDefinition.Features
            .Where(f => f.Status == FeatureStatus.Included)
            .OrderBy(f => f.Position)
            .ToList();

        var members = await _workspaceMemberRepository.GetByWorkspaceIdAsync(productDefinition.WorkspaceId);
        var skills = members
            .Where(m => m.Status == WorkspaceMemberStatus.Active)
            .Select(m => m.SkillLevel)
            .ToList();

        return (included, LockGatePolicy.Evaluate(included, skills), skills.Count);
    }
}
