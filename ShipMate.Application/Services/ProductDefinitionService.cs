using AutoMapper;
using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;
using ShipMate.Domain.Policies;

namespace ShipMate.Application.Services;

public class ProductDefinitionService : IProductDefinitionService
{
    private readonly IProductDefinitionRepository _productDefinitionRepository;
    private readonly IWorkspaceAccessGuard _workspaceAccessGuard;
    private readonly IMapper _mapper;

    public ProductDefinitionService(
        IProductDefinitionRepository productDefinitionRepository,
        IWorkspaceAccessGuard workspaceAccessGuard,
        IMapper mapper)
    {
        _productDefinitionRepository = productDefinitionRepository;
        _workspaceAccessGuard = workspaceAccessGuard;
        _mapper = mapper;
    }

    public async Task<ProductDefinitionDto> GetAsync(Guid userId, Guid workspaceId)
    {
        await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);

        var productDefinition = await _productDefinitionRepository.GetByWorkspaceIdAsync(workspaceId);

        // Created lazily on the first write, so a read never has to insert anything.
        if (productDefinition is null)
        {
            return new ProductDefinitionDto
            {
                WorkspaceId = workspaceId,
                Status = ProductDefinitionStatus.Draft
            };
        }

        return _mapper.Map<ProductDefinitionDto>(productDefinition);
    }

    public async Task<FeatureDto> AddCommittedFeatureAsync(
        Guid userId, Guid workspaceId, CreateCommittedFeatureRequest request)
    {
        await _workspaceAccessGuard.GetWorkspaceAsManagerAsync(userId, workspaceId);

        var productDefinition = await _productDefinitionRepository.GetByWorkspaceIdAsync(workspaceId);
        if (productDefinition is null)
        {
            productDefinition = new ProductDefinition
            {
                WorkspaceId = workspaceId,
                Status = ProductDefinitionStatus.Draft
            };
            await _productDefinitionRepository.AddAsync(productDefinition);
        }

        var sources = new List<FeatureOrigin> { FeatureOrigin.FromCommittedList };
        var origin = FeatureOriginPolicy.Resolve(sources);

        var feature = new Feature
        {
            ProductDefinitionId = productDefinition.Id,
            Name = request.Name,
            Description = request.Description,
            Sources = sources,
            Origin = origin,
            // Role and AI assessment stay empty until the AI analyzes this feature.
            Status = FeatureStatusPolicy.DefaultFor(origin, verdict: null),
            Position = productDefinition.NextFeaturePosition()
        };
        await _productDefinitionRepository.AddFeatureAsync(feature);

        productDefinition.UpdatedAt = DateTime.UtcNow;
        await _productDefinitionRepository.SaveChangesAsync();

        return _mapper.Map<FeatureDto>(feature);
    }

    public async Task<FeatureDto> UpdateCommittedFeatureAsync(
        Guid userId, Guid workspaceId, Guid featureId, UpdateCommittedFeatureRequest request)
    {
        var (productDefinition, feature) = await GetEditableCommittedFeatureOrThrow(userId, workspaceId, featureId);

        if (request.Name is not null)
        {
            feature.Name = request.Name;
        }

        if (request.Description is not null)
        {
            feature.Description = request.Description;
        }

        var now = DateTime.UtcNow;
        feature.UpdatedAt = now;
        productDefinition.UpdatedAt = now;
        await _productDefinitionRepository.SaveChangesAsync();

        return _mapper.Map<FeatureDto>(feature);
    }

    public async Task DeleteCommittedFeatureAsync(Guid userId, Guid workspaceId, Guid featureId)
    {
        var (productDefinition, feature) = await GetEditableCommittedFeatureOrThrow(userId, workspaceId, featureId);

        _productDefinitionRepository.RemoveFeature(feature);

        productDefinition.UpdatedAt = DateTime.UtcNow;
        await _productDefinitionRepository.SaveChangesAsync();
    }

    private async Task<(ProductDefinition ProductDefinition, Feature Feature)> GetEditableCommittedFeatureOrThrow(
        Guid userId, Guid workspaceId, Guid featureId)
    {
        await _workspaceAccessGuard.GetWorkspaceAsManagerAsync(userId, workspaceId);

        var productDefinition = await _productDefinitionRepository.GetByWorkspaceIdAsync(workspaceId)
            ?? throw new NotFoundException("Committed feature", featureId);

        var feature = productDefinition.Features
            .FirstOrDefault(f => f.Id == featureId && f.Origin == FeatureOrigin.FromCommittedList)
            ?? throw new NotFoundException("Committed feature", featureId);

        // Before the first AI analysis a committed feature is still raw input and can be fixed freely;
        // after that, edits and removals must go through the step 8 change request.
        if (await _productDefinitionRepository.HasSuccessfulAnalysisRunAsync(productDefinition.Id))
        {
            throw new CommittedFeatureLockedException();
        }

        return (productDefinition, feature);
    }
}
