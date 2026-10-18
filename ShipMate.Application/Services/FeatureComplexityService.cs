using AutoMapper;
using ShipMate.Application.Ai;
using ShipMate.Application.Ai.Lock;
using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Application.Services;

public class FeatureComplexityService : IFeatureComplexityService
{
    private readonly IProductDefinitionRepository _productDefinitionRepository;
    private readonly IWorkspaceAccessGuard _workspaceAccessGuard;
    private readonly IAiGateway _aiGateway;
    private readonly IMapper _mapper;

    public FeatureComplexityService(
        IProductDefinitionRepository productDefinitionRepository,
        IWorkspaceAccessGuard workspaceAccessGuard,
        IAiGateway aiGateway,
        IMapper mapper)
    {
        _productDefinitionRepository = productDefinitionRepository;
        _workspaceAccessGuard = workspaceAccessGuard;
        _aiGateway = aiGateway;
        _mapper = mapper;
    }

    public async Task<ProductDefinitionDto> ProposeAsync(
        Guid userId, Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var productDefinition = await GetScorableDefinitionOrThrow(userId, workspaceId);
        var included = IncludedFeatures(productDefinition);
        if (included.Count == 0)
        {
            return _mapper.Map<ProductDefinitionDto>(productDefinition);
        }

        var input = new ComplexityScoringInput
        {
            Problem = productDefinition.Problem,
            Solution = productDefinition.Solution,
            Features = included.Select(f => new ComplexityScoringFeatureInput
            {
                Id = f.Id.ToString(),
                Name = f.Name,
                Description = f.Description,
                Scope = f.Scope,
                Role = f.Role is null ? null : AiJson.NamingPolicy.ConvertName(f.Role.Value.ToString())
            }).ToList()
        };

        var output = await ComplexityScoringClient.ScoreAsync(_aiGateway, input, cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var score in output.Scores)
        {
            var feature = included.First(f => string.Equals(f.Id.ToString(), score.FeatureId, StringComparison.OrdinalIgnoreCase));
            feature.ProposedComplexity = score.Complexity;
            feature.UpdatedAt = now;
        }

        await _productDefinitionRepository.SaveChangesAsync();
        return _mapper.Map<ProductDefinitionDto>(productDefinition);
    }

    // Complexity is an input of LOCK, not of DEFINE, so scoring never sends READY_FOR_LOCK back to Draft.
    public async Task<ProductDefinitionDto> SetAsync(Guid userId, Guid workspaceId, SetComplexityRequest request)
    {
        var productDefinition = await GetScorableDefinitionOrThrow(userId, workspaceId);
        var included = IncludedFeatures(productDefinition);

        var now = DateTime.UtcNow;
        foreach (var item in request.Items)
        {
            var feature = included.FirstOrDefault(f => f.Id == item.FeatureId)
                ?? throw new NotFoundException("Included feature", item.FeatureId);
            feature.Complexity = item.Complexity;
            feature.UpdatedAt = now;
        }

        await _productDefinitionRepository.SaveChangesAsync();
        return _mapper.Map<ProductDefinitionDto>(productDefinition);
    }

    private async Task<ProductDefinition> GetScorableDefinitionOrThrow(Guid userId, Guid workspaceId)
    {
        await _workspaceAccessGuard.GetWorkspaceAsManagerAsync(userId, workspaceId);

        var productDefinition = await _productDefinitionRepository.GetByWorkspaceIdAsync(workspaceId);
        if (productDefinition is null
            || !await _productDefinitionRepository.HasSuccessfulAnalysisRunAsync(productDefinition.Id))
        {
            throw new ProductDefinitionNotAnalyzedException();
        }

        if (productDefinition.IsLocked)
        {
            throw new ProductDefinitionLockedException();
        }

        return productDefinition;
    }

    private static List<Feature> IncludedFeatures(ProductDefinition productDefinition) =>
        productDefinition.Features
            .Where(f => f.Status == FeatureStatus.Included)
            .OrderBy(f => f.Position)
            .ToList();
}
