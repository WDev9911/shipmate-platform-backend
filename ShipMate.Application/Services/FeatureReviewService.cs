using AutoMapper;
using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;
using ShipMate.Domain.Policies;

namespace ShipMate.Application.Services;

public class FeatureReviewService : IFeatureReviewService
{
    private readonly IProductDefinitionRepository _productDefinitionRepository;
    private readonly IWorkspaceAccessGuard _workspaceAccessGuard;
    private readonly IMapper _mapper;

    public FeatureReviewService(
        IProductDefinitionRepository productDefinitionRepository,
        IWorkspaceAccessGuard workspaceAccessGuard,
        IMapper mapper)
    {
        _productDefinitionRepository = productDefinitionRepository;
        _workspaceAccessGuard = workspaceAccessGuard;
        _mapper = mapper;
    }

    public async Task<ProductDefinitionDto> ChangeStatusAsync(
        Guid userId, Guid workspaceId, Guid featureId, UpdateFeatureStatusRequest request)
    {
        var (productDefinition, feature) = await GetReviewableFeatureOrThrow(userId, workspaceId, featureId);

        if (FeatureChangePolicy.RequiresChangeRequestForStatus(feature.Origin, request.Status))
        {
            throw new CommittedFeatureChangeRequiredException();
        }

        var now = DateTime.UtcNow;
        if (request.Status == FeatureStatus.Excluded)
        {
            Exclude(productDefinition, feature, request.DependentsResolution, now);
        }
        else
        {
            MarkDecided(feature, request.Status, now);
        }

        productDefinition.UpdatedAt = now;
        await _productDefinitionRepository.SaveChangesAsync();

        return _mapper.Map<ProductDefinitionDto>(productDefinition);
    }

    public async Task<FeatureDto> EditAsync(Guid userId, Guid workspaceId, Guid featureId, UpdateFeatureRequest request)
    {
        var (productDefinition, feature) = await GetReviewableFeatureOrThrow(userId, workspaceId, featureId);

        if (FeatureChangePolicy.RequiresChangeRequestForEdit(feature.Origin))
        {
            throw new CommittedFeatureChangeRequiredException();
        }

        if (request.Name is not null)
        {
            feature.Name = request.Name;
        }

        if (request.Description is not null)
        {
            feature.Description = request.Description;
        }

        if (request.Scope is not null)
        {
            feature.Scope = request.Scope;
        }

        var now = DateTime.UtcNow;
        MarkDecided(feature, feature.Status, now);
        productDefinition.UpdatedAt = now;
        await _productDefinitionRepository.SaveChangesAsync();

        return _mapper.Map<FeatureDto>(feature);
    }

    public async Task<ProductDefinitionDto> MergeAsync(Guid userId, Guid workspaceId, Guid featureId)
    {
        var (productDefinition, flagged, duplicateOf) = await GetDuplicatePairOrThrow(userId, workspaceId, featureId);

        var (survivor, absorbed) = FeatureMergePolicy.ChooseSurvivor(flagged, duplicateOf);
        var survivorBefore = FeatureContentSnapshot.Of(survivor);
        var absorbedBefore = FeatureContentSnapshot.Of(absorbed);

        survivor.Sources = survivor.Sources.Union(absorbed.Sources).ToList();
        survivor.Origin = FeatureOriginPolicy.Resolve(survivor.Sources);
        ClearDuplicateFlag(survivor);

        await MoveDependenciesAsync(productDefinition, absorbed, survivor);
        _productDefinitionRepository.RemoveFeature(absorbed);

        var now = DateTime.UtcNow;
        MarkDecided(survivor, survivor.Status, now);

        if (survivorBefore.Origin == FeatureOrigin.FromCommittedList || absorbedBefore.Origin == FeatureOrigin.FromCommittedList)
        {
            await _productDefinitionRepository.AddChangeLogAsync(new FeatureChangeLog
            {
                FeatureId = survivor.Id,
                Action = FeatureChangeAction.Merge,
                OldContent = FeatureContentSnapshot.ToJson([survivorBefore, absorbedBefore]),
                NewContent = FeatureContentSnapshot.Of(survivor).ToJson(),
                PerformedByUserId = userId
            });
        }

        productDefinition.UpdatedAt = now;
        await _productDefinitionRepository.SaveChangesAsync();

        return _mapper.Map<ProductDefinitionDto>(productDefinition);
    }

    public async Task<ProductDefinitionDto> KeepSeparateAsync(Guid userId, Guid workspaceId, Guid featureId)
    {
        var (productDefinition, flagged, duplicateOf) = await GetDuplicatePairOrThrow(userId, workspaceId, featureId);

        ClearDuplicateFlag(flagged);

        var now = DateTime.UtcNow;
        MarkDecided(flagged, flagged.Status, now);
        MarkDecided(duplicateOf, duplicateOf.Status, now);

        productDefinition.UpdatedAt = now;
        await _productDefinitionRepository.SaveChangesAsync();

        return _mapper.Map<ProductDefinitionDto>(productDefinition);
    }

    public async Task<FeatureDto> AcceptPersonaConflictAsync(Guid userId, Guid workspaceId, Guid featureId)
    {
        var (productDefinition, feature) = await GetReviewableFeatureOrThrow(userId, workspaceId, featureId);

        if (!feature.PersonaConflict)
        {
            throw new FeatureNotFlaggedAsPersonaConflictException();
        }

        feature.PersonaConflict = false;
        feature.PersonaConflictReason = null;

        var now = DateTime.UtcNow;
        MarkDecided(feature, feature.Status, now);
        productDefinition.UpdatedAt = now;
        await _productDefinitionRepository.SaveChangesAsync();

        return _mapper.Map<FeatureDto>(feature);
    }

    // Re-points every dependency link touching the absorbed feature onto the survivor, skipping links that
    // would duplicate an existing one or make the survivor depend on itself.
    private async Task MoveDependenciesAsync(ProductDefinition productDefinition, Feature absorbed, Feature survivor)
    {
        foreach (var link in absorbed.Dependencies.ToList())
        {
            var prerequisiteId = link.DependsOnFeatureId;
            if (prerequisiteId != survivor.Id && survivor.Dependencies.All(d => d.DependsOnFeatureId != prerequisiteId))
            {
                await _productDefinitionRepository.AddDependencyAsync(new FeatureDependency
                {
                    FeatureId = survivor.Id,
                    DependsOnFeatureId = prerequisiteId,
                    Reason = link.Reason
                });
            }

            _productDefinitionRepository.RemoveDependency(link);
        }

        foreach (var dependent in productDefinition.Features.Where(f => f.Id != absorbed.Id).ToList())
        {
            var link = dependent.Dependencies.FirstOrDefault(d => d.DependsOnFeatureId == absorbed.Id);
            if (link is null)
            {
                continue;
            }

            if (dependent.Id != survivor.Id && dependent.Dependencies.All(d => d.DependsOnFeatureId != survivor.Id))
            {
                await _productDefinitionRepository.AddDependencyAsync(new FeatureDependency
                {
                    FeatureId = dependent.Id,
                    DependsOnFeatureId = survivor.Id,
                    Reason = link.Reason
                });
            }

            _productDefinitionRepository.RemoveDependency(link);
        }
    }

    private async Task<(ProductDefinition ProductDefinition, Feature Flagged, Feature DuplicateOf)> GetDuplicatePairOrThrow(
        Guid userId, Guid workspaceId, Guid featureId)
    {
        var (productDefinition, flagged) = await GetReviewableFeatureOrThrow(userId, workspaceId, featureId);

        var duplicateOf = flagged.PossibleDuplicate
            ? productDefinition.Features.FirstOrDefault(f => f.Id == flagged.DuplicateOfId)
            : null;

        if (duplicateOf is null)
        {
            throw new FeatureNotFlaggedAsDuplicateException();
        }

        return (productDefinition, flagged, duplicateOf);
    }

    private static void ClearDuplicateFlag(Feature feature)
    {
        feature.PossibleDuplicate = false;
        feature.DuplicateOfId = null;
        feature.DuplicateReason = null;
    }

    // DEFINE step 5: excluding a feature that included features depend on forces the developer to choose
    // between dropping those links or excluding the dependents too — never leave a dangling dependency.
    private void Exclude(
        ProductDefinition productDefinition, Feature feature, DependentsResolution? resolution, DateTime now)
    {
        var dependents = FindIncludedDependents(productDefinition, feature);
        if (dependents.Count == 0)
        {
            MarkDecided(feature, FeatureStatus.Excluded, now);
            return;
        }

        switch (resolution)
        {
            case DependentsResolution.RemoveLinks:
                foreach (var dependent in productDefinition.Features)
                {
                    var link = dependent.Dependencies.FirstOrDefault(d => d.DependsOnFeatureId == feature.Id);
                    if (link is not null && dependent.Status == FeatureStatus.Included)
                    {
                        _productDefinitionRepository.RemoveDependency(link);
                        MarkDecided(dependent, dependent.Status, now);
                    }
                }
                break;

            case DependentsResolution.ExcludeDependents:
                if (dependents.Any(d => FeatureChangePolicy.RequiresChangeRequestForStatus(d.Origin, FeatureStatus.Excluded)))
                {
                    throw new CommittedFeatureChangeRequiredException();
                }

                foreach (var dependent in dependents)
                {
                    MarkDecided(dependent, FeatureStatus.Excluded, now);
                }
                break;

            default:
                throw new FeatureHasDependentsException(
                    dependents.Select(d => new DependentFeatureDto(d.Id, d.Name)).ToList());
        }

        MarkDecided(feature, FeatureStatus.Excluded, now);
    }

    // Every included feature that depends on this one, directly or through a chain of included features.
    private static List<Feature> FindIncludedDependents(ProductDefinition productDefinition, Feature feature)
    {
        var result = new List<Feature>();
        var visited = new HashSet<Guid> { feature.Id };
        var queue = new Queue<Guid>([feature.Id]);

        while (queue.Count > 0)
        {
            var prerequisiteId = queue.Dequeue();
            var directDependents = productDefinition.Features.Where(f =>
                f.Status == FeatureStatus.Included
                && f.Dependencies.Any(d => d.DependsOnFeatureId == prerequisiteId));

            foreach (var dependent in directDependents.Where(d => visited.Add(d.Id)))
            {
                result.Add(dependent);
                queue.Enqueue(dependent.Id);
            }
        }

        return result;
    }

    private static void MarkDecided(Feature feature, FeatureStatus status, DateTime now)
    {
        feature.Status = status;
        feature.DevDecided = true;
        feature.UpdatedAt = now;
    }

    private async Task<(ProductDefinition ProductDefinition, Feature Feature)> GetReviewableFeatureOrThrow(
        Guid userId, Guid workspaceId, Guid featureId)
    {
        await _workspaceAccessGuard.GetWorkspaceAsManagerAsync(userId, workspaceId);

        var productDefinition = await _productDefinitionRepository.GetByWorkspaceIdAsync(workspaceId)
            ?? throw new NotFoundException("Feature", featureId);

        if (!await _productDefinitionRepository.HasSuccessfulAnalysisRunAsync(productDefinition.Id))
        {
            throw new ProductDefinitionNotAnalyzedException();
        }

        var feature = productDefinition.Features.FirstOrDefault(f => f.Id == featureId)
            ?? throw new NotFoundException("Feature", featureId);

        return (productDefinition, feature);
    }
}
