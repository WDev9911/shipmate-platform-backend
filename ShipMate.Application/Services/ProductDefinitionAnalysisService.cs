using System.Text.Json;
using AutoMapper;
using Microsoft.Extensions.Logging;
using ShipMate.Application.Ai;
using ShipMate.Application.Ai.Define;
using ShipMate.Application.DTOs.Ai;
using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;
using ShipMate.Domain.Policies;
using ShipMate.Domain.ValueObjects;

namespace ShipMate.Application.Services;

public class ProductDefinitionAnalysisService : IProductDefinitionAnalysisService
{
    private static readonly Lazy<string> SystemInstruction =
        new(() => AiResources.ReadText(DefinePrompts.SystemInstructionFile));

    private static readonly Lazy<string> OutputSchema = new(() => AiSchemaBuilder.Build(
        AiResources.ReadText(DefinePrompts.SchemaFile),
        typeof(FeatureRole), typeof(FeatureOrigin), typeof(AiVerdict)));

    private readonly IProductDefinitionRepository _productDefinitionRepository;
    private readonly IWorkspaceAccessGuard _workspaceAccessGuard;
    private readonly IAiGateway _aiGateway;
    private readonly IMapper _mapper;
    private readonly ILogger<ProductDefinitionAnalysisService> _logger;

    public ProductDefinitionAnalysisService(
        IProductDefinitionRepository productDefinitionRepository,
        IWorkspaceAccessGuard workspaceAccessGuard,
        IAiGateway aiGateway,
        IMapper mapper,
        ILogger<ProductDefinitionAnalysisService> logger)
    {
        _productDefinitionRepository = productDefinitionRepository;
        _workspaceAccessGuard = workspaceAccessGuard;
        _aiGateway = aiGateway;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<ProductDefinitionDto> AnalyzeAsync(
        Guid userId, Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceAccessGuard.GetWorkspaceAsManagerAsync(userId, workspaceId);

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
        else if (await _productDefinitionRepository.HasSuccessfulAnalysisRunAsync(productDefinition.Id))
        {
            // Re-running (DEFINE step 5b) must preserve the developer's decisions — that's milestone M8.
            throw new ProductDefinitionAlreadyAnalyzedException();
        }

        var committedFeatures = productDefinition.Features
            .Where(f => f.Origin == FeatureOrigin.FromCommittedList)
            .OrderBy(f => f.Position)
            .ToList();

        var input = new DefineAnalysisInput
        {
            VisionPrompt = workspace.VisionPrompt,
            CommittedFeatures = committedFeatures
                .Select(f => new DefineCommittedFeatureInput
                {
                    Id = f.Id.ToString(),
                    Name = f.Name,
                    Description = f.Description
                })
                .ToList()
        };

        var run = new AiAnalysisRun
        {
            ProductDefinitionId = productDefinition.Id,
            RunByUserId = userId,
            VisionPromptSnapshot = workspace.VisionPrompt,
            Model = _aiGateway.Model
        };
        await _productDefinitionRepository.AddAnalysisRunAsync(run);

        var output = await RequestAnalysisAsync(input, run, cancellationToken);

        await ApplyOutputAsync(productDefinition, committedFeatures, output);

        run.Succeeded = true;
        productDefinition.UpdatedAt = DateTime.UtcNow;
        await _productDefinitionRepository.SaveChangesAsync();

        return _mapper.Map<ProductDefinitionDto>(productDefinition);
    }

    // Calls the AI and returns a validated result. Every failure is recorded on the run before rethrowing,
    // so a failed attempt is still traceable, but no feature is ever saved from a bad result.
    private async Task<DefineAnalysisOutput> RequestAnalysisAsync(
        DefineAnalysisInput input, AiAnalysisRun run, CancellationToken cancellationToken)
    {
        AiJsonResponse response;
        try
        {
            response = await _aiGateway.GenerateJsonAsync(new AiJsonRequest
            {
                SystemInstruction = SystemInstruction.Value,
                UserPrompt = JsonSerializer.Serialize(input, AiJson.Options),
                JsonSchema = OutputSchema.Value
            }, cancellationToken);
        }
        catch (AppException ex) when (ex is AiRateLimitedException or AiProviderUnavailableException or AiOutputInvalidException)
        {
            await RecordFailedRunAsync(run, ex.ErrorCode, (ex as AiOutputInvalidException)?.RawResponse);
            throw;
        }

        run.Model = response.Model;
        run.RawResponse = response.Json;

        DefineAnalysisOutput? output = null;
        IReadOnlyList<string> errors;
        try
        {
            output = JsonSerializer.Deserialize<DefineAnalysisOutput>(response.Json, AiJson.Options);
            errors = output is null
                ? ["The AI returned an empty result."]
                : DefineAnalysisOutputValidator.Validate(output, input.CommittedFeatures.Select(f => f.Id).ToList());
        }
        catch (JsonException ex)
        {
            errors = [$"The AI returned malformed JSON: {ex.Message}"];
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("DEFINE analysis rejected for run {RunId}: {Errors}", run.Id, string.Join(" | ", errors));
            await RecordFailedRunAsync(run, string.Join(Environment.NewLine, errors), response.Json);
            throw new AiOutputInvalidException(response.Json);
        }

        return output!;
    }

    private async Task RecordFailedRunAsync(AiAnalysisRun run, string errorMessage, string? rawResponse)
    {
        run.Succeeded = false;
        run.ErrorMessage = errorMessage;
        run.RawResponse = rawResponse;
        await _productDefinitionRepository.SaveChangesAsync();
    }

    private async Task ApplyOutputAsync(
        ProductDefinition productDefinition, List<Feature> committedFeatures, DefineAnalysisOutput output)
    {
        productDefinition.LockedPersona = new LockedPersona
        {
            PrimaryPersona = output.Persona.PrimaryPersona,
            Reason = output.Persona.Reason,
            SupportingRoles = output.Persona.SupportingRoles
                .Select(role => new SupportingRole { Name = role.Name, Reason = role.Reason })
                .ToList()
        };
        productDefinition.Problem = output.ProblemSolution.Problem;
        productDefinition.Solution = output.ProblemSolution.Solution;

        var now = DateTime.UtcNow;
        var committedById = committedFeatures.ToDictionary(f => f.Id.ToString(), StringComparer.OrdinalIgnoreCase);
        var featuresByAiId = new Dictionary<string, Feature>(StringComparer.OrdinalIgnoreCase);
        var position = productDefinition.NextFeaturePosition();

        foreach (var item in output.Features)
        {
            if (committedById.TryGetValue(item.Id, out var committed))
            {
                // Only the AI's judgement is taken; the agreed content (name, description, status) stays as entered.
                committed.Role = item.Role;
                committed.AiVerdict = item.AiAssessment.Verdict;
                committed.AiReason = item.AiAssessment.Reason;
                committed.PersonaConflict = item.PersonaConflict;
                committed.PersonaConflictReason = item.PersonaConflict ? item.PersonaConflictReason : null;
                committed.UpdatedAt = now;
                featuresByAiId[item.Id] = committed;
                continue;
            }

            var sources = new List<FeatureOrigin> { item.Origin };
            var origin = FeatureOriginPolicy.Resolve(sources);

            var feature = new Feature
            {
                ProductDefinitionId = productDefinition.Id,
                Name = item.Name,
                Description = item.Description,
                Scope = item.Scope,
                Role = item.Role,
                Sources = sources,
                Origin = origin,
                AiVerdict = item.AiAssessment.Verdict,
                AiReason = item.AiAssessment.Reason,
                Status = FeatureStatusPolicy.DefaultFor(origin, item.AiAssessment.Verdict),
                PossibleDuplicate = item.PossibleDuplicate,
                DuplicateOfId = item.PossibleDuplicate ? committedById[item.DuplicateOf!].Id : null,
                DuplicateReason = item.PossibleDuplicate ? item.DuplicateReason : null,
                Position = position++
            };
            await _productDefinitionRepository.AddFeatureAsync(feature);
            featuresByAiId[item.Id] = feature;
        }

        foreach (var item in output.Features)
        {
            foreach (var dependency in item.DependsOn.DistinctBy(d => d.FeatureId, StringComparer.OrdinalIgnoreCase))
            {
                await _productDefinitionRepository.AddDependencyAsync(new FeatureDependency
                {
                    FeatureId = featuresByAiId[item.Id].Id,
                    DependsOnFeatureId = featuresByAiId[dependency.FeatureId].Id,
                    Reason = dependency.Reason
                });
            }
        }
    }
}
