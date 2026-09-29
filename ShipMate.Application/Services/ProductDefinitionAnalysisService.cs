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

    public async Task<AnalyzeProductDefinitionResponse> AnalyzeAsync(
        Guid userId, Guid workspaceId, AnalyzeProductDefinitionRequest? request, CancellationToken cancellationToken = default)
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

        var isReanalysis = await _productDefinitionRepository.HasSuccessfulAnalysisRunAsync(productDefinition.Id);
        var features = productDefinition.Features.OrderBy(f => f.Position).ToList();

        var input = new DefineAnalysisInput
        {
            VisionPrompt = workspace.VisionPrompt,
            CommittedFeatures = features
                .Where(f => f.Origin == FeatureOrigin.FromCommittedList)
                .Select(f => new DefineCommittedFeatureInput
                {
                    Id = f.Id.ToString(),
                    Name = f.Name,
                    Description = f.Description
                })
                .ToList(),
            ExistingFeatures = isReanalysis ? features.Select(ToExistingFeatureInput).ToList() : [],
            CurrentPersona = isReanalysis ? ToPersonaInput(productDefinition.LockedPersona) : null,
            Instruction = request?.Instruction,
            ReassessPersona = request?.ReassessPersona ?? false
        };

        var run = new AiAnalysisRun
        {
            ProductDefinitionId = productDefinition.Id,
            RunByUserId = userId,
            VisionPromptSnapshot = workspace.VisionPrompt,
            Model = _aiGateway.Model
        };
        await _productDefinitionRepository.AddAnalysisRunAsync(run);

        var keptFeatureIds = features
            .Where(f => !FeatureRegenerationPolicy.CanRegenerate(f))
            .Select(f => f.Id.ToString())
            .ToList();

        var output = await RequestAnalysisAsync(input, keptFeatureIds, run, cancellationToken);

        var removedDependencies = await ApplyOutputAsync(productDefinition, features, output);

        run.Succeeded = true;
        productDefinition.MarkModified(DateTime.UtcNow);
        await _productDefinitionRepository.SaveChangesAsync();

        return new AnalyzeProductDefinitionResponse
        {
            ProductDefinition = _mapper.Map<ProductDefinitionDto>(productDefinition),
            RemovedDependencies = removedDependencies
        };
    }

    // Calls the AI and returns a validated result. Every failure is recorded on the run before rethrowing,
    // so a failed attempt is still traceable, but no feature is ever saved from a bad result.
    private async Task<DefineAnalysisOutput> RequestAnalysisAsync(
        DefineAnalysisInput input, IReadOnlyCollection<string> keptFeatureIds, AiAnalysisRun run,
        CancellationToken cancellationToken)
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
                : DefineAnalysisOutputValidator.Validate(
                    output, input.CommittedFeatures.Select(f => f.Id).ToList(), keptFeatureIds);
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

    // DEFINE step 5b. A first analysis is simply the case where only committed features exist and none is
    // regenerable, so one merge handles both.
    private async Task<List<RemovedDependencyDto>> ApplyOutputAsync(
        ProductDefinition productDefinition, List<Feature> features, DefineAnalysisOutput output)
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
        var committedById = features
            .Where(f => f.Origin == FeatureOrigin.FromCommittedList)
            .ToDictionary(f => f.Id.ToString(), StringComparer.OrdinalIgnoreCase);
        var keptById = features
            .Where(f => !FeatureRegenerationPolicy.CanRegenerate(f))
            .ToDictionary(f => f.Id.ToString(), StringComparer.OrdinalIgnoreCase);
        var regenerableById = features
            .Where(FeatureRegenerationPolicy.CanRegenerate)
            .ToDictionary(f => f.Id.ToString(), StringComparer.OrdinalIgnoreCase);

        // Kept features are referenceable even when the AI leaves them out of its output.
        var featuresByAiId = new Dictionary<string, Feature>(keptById, StringComparer.OrdinalIgnoreCase);
        var position = productDefinition.NextFeaturePosition();

        foreach (var item in output.Features)
        {
            if (keptById.TryGetValue(item.Id, out var kept))
            {
                ApplyToKeptFeature(kept, item, committedById);
                kept.UpdatedAt = now;
                continue;
            }

            if (regenerableById.Remove(item.Id, out var regenerated))
            {
                ApplyAiContent(regenerated, item, committedById);
                regenerated.UpdatedAt = now;
                featuresByAiId[item.Id] = regenerated;
                continue;
            }

            var feature = new Feature
            {
                ProductDefinitionId = productDefinition.Id,
                Position = position++
            };
            ApplyAiContent(feature, item, committedById);
            await _productDefinitionRepository.AddFeatureAsync(feature);
            featuresByAiId[item.Id] = feature;
        }

        // Regenerable features the AI left out of this run are dropped.
        var droppedIds = regenerableById.Values.Select(f => f.Id).ToHashSet();
        var keptIds = keptById.Values.Select(f => f.Id).ToHashSet();

        var removedDependencies = await RebuildDependenciesAsync(features, featuresByAiId, output, keptIds, droppedIds);

        foreach (var dropped in regenerableById.Values)
        {
            _productDefinitionRepository.RemoveFeature(dropped);
        }

        return removedDependencies;
    }

    // A kept feature keeps its content, origin and status. The AI may always re-assess it. It may also set the
    // role and flags until the developer has decided on the feature — except on a feature the AI has never
    // judged (e.g. a committed feature added and included since the last run), which still gets its role and
    // persona check.
    private static void ApplyToKeptFeature(Feature kept, DefineFeatureOutput item, IReadOnlyDictionary<string, Feature> committedById)
    {
        var neverAssessed = kept.AiVerdict is null;

        kept.AiVerdict = item.AiAssessment.Verdict;
        kept.AiReason = item.AiAssessment.Reason;

        if (kept.DevDecided && !neverAssessed)
        {
            return;
        }

        kept.Role = item.Role;

        if (kept.Origin == FeatureOrigin.FromCommittedList)
        {
            kept.PersonaConflict = item.PersonaConflict;
            kept.PersonaConflictReason = item.PersonaConflict ? item.PersonaConflictReason : null;
        }
        else
        {
            ApplyDuplicateFlag(kept, item, committedById);
        }
    }

    // A regenerated or new feature takes everything from the AI; its status is recomputed from DEFINE step 7.
    private static void ApplyAiContent(Feature feature, DefineFeatureOutput item, IReadOnlyDictionary<string, Feature> committedById)
    {
        var sources = new List<FeatureOrigin> { item.Origin };
        var origin = FeatureOriginPolicy.Resolve(sources);

        feature.Name = item.Name;
        feature.Description = item.Description;
        feature.Scope = item.Scope;
        feature.Role = item.Role;
        feature.Sources = sources;
        feature.Origin = origin;
        feature.AiVerdict = item.AiAssessment.Verdict;
        feature.AiReason = item.AiAssessment.Reason;
        feature.Status = FeatureStatusPolicy.DefaultFor(origin, item.AiAssessment.Verdict);
        ApplyDuplicateFlag(feature, item, committedById);
    }

    private static void ApplyDuplicateFlag(Feature feature, DefineFeatureOutput item, IReadOnlyDictionary<string, Feature> committedById)
    {
        feature.PossibleDuplicate = item.PossibleDuplicate;
        feature.DuplicateOfId = item.PossibleDuplicate ? committedById[item.DuplicateOf!].Id : null;
        feature.DuplicateReason = item.PossibleDuplicate ? item.DuplicateReason : null;
    }

    // Links between two kept features are the developer's graph and stay exactly as they are. Every other link
    // was proposed by the AI and is rebuilt from this run's output. Links pointing to a dropped feature are
    // reported so the developer can see what disappeared.
    private async Task<List<RemovedDependencyDto>> RebuildDependenciesAsync(
        List<Feature> existingFeatures,
        IReadOnlyDictionary<string, Feature> featuresByAiId,
        DefineAnalysisOutput output,
        HashSet<Guid> keptIds,
        HashSet<Guid> droppedIds)
    {
        var proposedLinks = new Dictionary<(Guid FeatureId, Guid PrerequisiteId), string>();
        foreach (var item in output.Features)
        {
            var featureId = featuresByAiId[item.Id].Id;
            foreach (var dependency in item.DependsOn)
            {
                var prerequisiteId = featuresByAiId[dependency.FeatureId].Id;
                if (featureId != prerequisiteId && !(keptIds.Contains(featureId) && keptIds.Contains(prerequisiteId)))
                {
                    proposedLinks.TryAdd((featureId, prerequisiteId), dependency.Reason);
                }
            }
        }

        var namesById = existingFeatures.ToDictionary(f => f.Id, f => f.Name);
        var removedDependencies = new List<RemovedDependencyDto>();

        foreach (var link in existingFeatures.SelectMany(f => f.Dependencies).ToList())
        {
            if (keptIds.Contains(link.FeatureId) && keptIds.Contains(link.DependsOnFeatureId))
            {
                continue;
            }

            // Re-proposed links are updated in place rather than deleted and re-inserted.
            if (proposedLinks.Remove((link.FeatureId, link.DependsOnFeatureId), out var reason))
            {
                link.Reason = reason;
                continue;
            }

            if (!droppedIds.Contains(link.FeatureId) && droppedIds.Contains(link.DependsOnFeatureId))
            {
                removedDependencies.Add(new RemovedDependencyDto(
                    link.FeatureId, namesById[link.FeatureId], namesById[link.DependsOnFeatureId]));
            }

            _productDefinitionRepository.RemoveDependency(link);
        }

        foreach (var ((featureId, prerequisiteId), reason) in proposedLinks)
        {
            await _productDefinitionRepository.AddDependencyAsync(new FeatureDependency
            {
                FeatureId = featureId,
                DependsOnFeatureId = prerequisiteId,
                Reason = reason
            });
        }

        return removedDependencies;
    }

    private static DefineExistingFeatureInput ToExistingFeatureInput(Feature feature) => new()
    {
        Id = feature.Id.ToString(),
        Name = feature.Name,
        Description = feature.Description,
        Scope = feature.Scope,
        Role = feature.Role,
        Origin = feature.Origin,
        Status = feature.Status,
        AiVerdict = feature.AiVerdict,
        CanRegenerate = FeatureRegenerationPolicy.CanRegenerate(feature),
        DependsOn = feature.Dependencies
            .Select(d => new DefineDependencyOutput { FeatureId = d.DependsOnFeatureId.ToString(), Reason = d.Reason ?? string.Empty })
            .ToList()
    };

    private static DefinePersona? ToPersonaInput(LockedPersona? persona) => persona is null
        ? null
        : new DefinePersona
        {
            PrimaryPersona = persona.PrimaryPersona,
            Reason = persona.Reason,
            SupportingRoles = persona.SupportingRoles
                .Select(role => new DefineSupportingRole { Name = role.Name, Reason = role.Reason })
                .ToList()
        };
}
