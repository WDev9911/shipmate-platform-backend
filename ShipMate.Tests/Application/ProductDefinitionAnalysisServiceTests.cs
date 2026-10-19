using System.Text.Json;
using AutoMapper;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ShipMate.Application.Ai;
using ShipMate.Application.Ai.Define;
using ShipMate.Application.DTOs.Ai;
using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests.Application;

// DEFINE M3 (first analysis) and M8 (re-analysis). The AI is replaced by a scripted answer, so no tokens are spent.
public class ProductDefinitionAnalysisServiceTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _workspaceId = Guid.NewGuid();
    private readonly IWorkspaceAccessGuard _guard = Substitute.For<IWorkspaceAccessGuard>();
    private readonly IAiGateway _ai = Substitute.For<IAiGateway>();
    private AiJsonRequest? _lastRequest;

    public ProductDefinitionAnalysisServiceTests()
    {
        _guard.GetWorkspaceAsManagerAsync(_userId, _workspaceId)
            .Returns(new Workspace { VisionPrompt = "Bệnh nhân tự đặt lịch khám online" });
    }

    private InMemoryDefinitions Repository(ProductDefinition definition, bool analyzedBefore) => new(definition, analyzedBefore);

    private ProductDefinitionAnalysisService Service(InMemoryDefinitions repository) =>
        new(repository, _guard, _ai, Substitute.For<IMapper>(), Substitute.For<ILogger<ProductDefinitionAnalysisService>>());

    private void AiAnswers(DefineAnalysisOutput output) => AiAnswersJson(JsonSerializer.Serialize(output, AiJson.Options));

    private void AiAnswersJson(string json) =>
        _ai.GenerateJsonAsync(Arg.Do<AiJsonRequest>(r => _lastRequest = r), Arg.Any<CancellationToken>())
            .Returns(new AiJsonResponse { Json = json, Model = "fake-model" });

    private Task<AnalyzeProductDefinitionResponse> Analyze(InMemoryDefinitions repository, AnalyzeProductDefinitionRequest? request = null) =>
        Service(repository).AnalyzeAsync(_userId, _workspaceId, request);

    private static DefineFeatureOutput Item(
        string id,
        string name,
        FeatureOrigin origin = FeatureOrigin.FromAi,
        FeatureRole role = FeatureRole.Primary,
        AiVerdict verdict = AiVerdict.Required,
        params string[] dependsOn) => new()
    {
        Id = id,
        Name = name,
        Description = $"{name} (AI)",
        Scope = "scope",
        Role = role,
        Origin = origin,
        AiAssessment = new DefineAiAssessmentOutput { Verdict = verdict, Reason = "AI reason" },
        DependsOn = dependsOn.Select(d => new DefineDependencyOutput { FeatureId = d, Reason = $"needs {d}" }).ToList()
    };

    private static DefineAnalysisOutput Output(params DefineFeatureOutput[] features) => new()
    {
        Persona = new DefinePersona
        {
            PrimaryPersona = "Bệnh nhân",
            Reason = "đặt lịch",
            SupportingRoles = [new DefineSupportingRole { Name = "Bác sĩ", Reason = "tạo lịch" }]
        },
        ProblemSolution = new DefineProblemSolutionOutput { Problem = "gọi điện mất thời gian", Solution = "đặt lịch online" },
        Features = features.ToList()
    };

    private static string Id(Feature feature) => feature.Id.ToString();

    // ---------- First analysis ----------

    [Fact]
    public async Task FirstAnalysis_KeepsTheCommittedContent_AndCreatesTheAiFeaturesWithTheirDefaultStatus()
    {
        var committed = TestData.Feature("Đặt lịch khám", FeatureOrigin.FromCommittedList, FeatureStatus.PendingConfirmation, verdict: null);
        committed.Role = null;
        var definition = TestData.Definition(committed);
        var repository = Repository(definition, analyzedBefore: false);

        var committedItem = Item(Id(committed), "Tên AI đổi", FeatureOrigin.FromCommittedList, dependsOn: "n1");
        committedItem.PersonaConflict = true;
        committedItem.PersonaConflictReason = "lệch persona";
        AiAnswers(Output(
            committedItem,
            Item("n1", "Quản lý lịch bác sĩ", FeatureOrigin.FromCustomerMentioned, FeatureRole.Supporting),
            Item("n2", "Đăng nhập"),
            Item("n3", "Chat", verdict: AiVerdict.NotRequired)));

        await Analyze(repository);

        // Committed feature: content and status as the developer entered them; role and persona check from the AI.
        Assert.Equal("Đặt lịch khám", committed.Name);
        Assert.Equal(FeatureStatus.PendingConfirmation, committed.Status);
        Assert.Equal(FeatureRole.Primary, committed.Role);
        Assert.True(committed.PersonaConflict);
        Assert.Equal("lệch persona", committed.PersonaConflictReason);

        // New features: status from DEFINE step 7, sources = [origin].
        var byName = definition.Features.ToDictionary(f => f.Name);
        Assert.Equal(4, byName.Count);
        Assert.Equal(FeatureStatus.PendingConfirmation, byName["Quản lý lịch bác sĩ"].Status);
        Assert.Equal(FeatureStatus.Included, byName["Đăng nhập"].Status);
        Assert.Equal(FeatureStatus.Excluded, byName["Chat"].Status);
        Assert.Equal([FeatureOrigin.FromAi], byName["Đăng nhập"].Sources);

        // AI ids are mapped to the saved features.
        var link = Assert.Single(repository.AddedDependencies);
        Assert.Equal(committed.Id, link.FeatureId);
        Assert.Equal(byName["Quản lý lịch bác sĩ"].Id, link.DependsOnFeatureId);
        Assert.Equal("needs n1", link.Reason);

        // Persona, problem/solution and the run are saved.
        Assert.Equal("Bệnh nhân", definition.LockedPersona!.PrimaryPersona);
        Assert.Equal("Bác sĩ", Assert.Single(definition.LockedPersona.SupportingRoles).Name);
        Assert.Equal("đặt lịch online", definition.Solution);
        var run = Assert.Single(repository.Runs);
        Assert.True(run.Succeeded);
        Assert.Equal("fake-model", run.Model);
        Assert.Equal("Bệnh nhân tự đặt lịch khám online", run.VisionPromptSnapshot);
        Assert.NotNull(run.RawResponse);
        Assert.True(repository.Saves > 0);
    }

    [Fact]
    public async Task FirstAnalysis_DuplicateFlagPointsToTheCommittedFeature()
    {
        var committed = TestData.Feature("Đặt lịch khám", FeatureOrigin.FromCommittedList, FeatureStatus.PendingConfirmation, verdict: null);
        var definition = TestData.Definition(committed);
        var repository = Repository(definition, analyzedBefore: false);
        var duplicate = Item("n1", "Đặt lịch hẹn");
        duplicate.PossibleDuplicate = true;
        duplicate.DuplicateOf = Id(committed);
        duplicate.DuplicateReason = "cùng chức năng";
        AiAnswers(Output(Item(Id(committed), "x", FeatureOrigin.FromCommittedList), duplicate));

        await Analyze(repository);

        var saved = definition.Features.Single(f => f.Name == "Đặt lịch hẹn");
        Assert.True(saved.PossibleDuplicate);
        Assert.Equal(committed.Id, saved.DuplicateOfId);
        Assert.Equal("cùng chức năng", saved.DuplicateReason);
    }

    [Fact]
    public async Task FirstAnalysis_SendsNoExistingFeaturesOrPersona()
    {
        var committed = TestData.Feature("Đặt lịch khám", FeatureOrigin.FromCommittedList, FeatureStatus.PendingConfirmation, verdict: null);
        var repository = Repository(TestData.Definition(committed), analyzedBefore: false);
        AiAnswers(Output(Item(Id(committed), "x", FeatureOrigin.FromCommittedList)));

        await Analyze(repository);

        var input = JsonSerializer.Deserialize<DefineAnalysisInput>(_lastRequest!.UserPrompt, AiJson.Options)!;
        Assert.Equal("Đặt lịch khám", Assert.Single(input.CommittedFeatures).Name);
        Assert.Empty(input.ExistingFeatures);
        Assert.Null(input.CurrentPersona);
    }

    // ---------- Invalid AI results: nothing saved, but the run is recorded ----------

    [Fact]
    public async Task MalformedJson_IsRejected_AndOnlyTheFailedRunIsKept()
    {
        var committed = TestData.Feature("Đặt lịch khám", FeatureOrigin.FromCommittedList, FeatureStatus.PendingConfirmation, verdict: null);
        committed.Role = null;
        var definition = TestData.Definition(committed);
        var repository = Repository(definition, analyzedBefore: false);
        AiAnswersJson("{ not json");

        await Assert.ThrowsAsync<AiOutputInvalidException>(() => Analyze(repository));

        var run = Assert.Single(repository.Runs);
        Assert.False(run.Succeeded);
        Assert.Contains("malformed JSON", run.ErrorMessage);
        Assert.Equal("{ not json", run.RawResponse);
        Assert.Single(definition.Features);
        Assert.Null(definition.LockedPersona);
        Assert.Null(committed.Role);
    }

    [Fact]
    public async Task OutputBreakingASpecRule_IsRejected_WithTheReasonOnTheRun()
    {
        var committed = TestData.Feature("Đặt lịch khám", FeatureOrigin.FromCommittedList, FeatureStatus.PendingConfirmation, verdict: null);
        var definition = TestData.Definition(committed);
        var repository = Repository(definition, analyzedBefore: false);
        AiAnswers(Output(
            Item(Id(committed), "x", FeatureOrigin.FromCommittedList),
            Item("n1", "Bác sĩ tạo lịch", role: FeatureRole.Supporting)));

        await Assert.ThrowsAsync<AiOutputInvalidException>(() => Analyze(repository));

        Assert.Contains("Supporting feature 'n1' is not required by any primary feature", Assert.Single(repository.Runs).ErrorMessage);
        Assert.Single(definition.Features);
        Assert.Empty(repository.AddedDependencies);
    }

    [Fact]
    public async Task AiRateLimited_IsRecordedOnTheRunAndRethrown()
    {
        var repository = Repository(TestData.Definition(), analyzedBefore: false);
        _ai.GenerateJsonAsync(Arg.Any<AiJsonRequest>(), Arg.Any<CancellationToken>()).Throws(new AiRateLimitedException());

        await Assert.ThrowsAsync<AiRateLimitedException>(() => Analyze(repository));

        var run = Assert.Single(repository.Runs);
        Assert.False(run.Succeeded);
        Assert.Equal("AI_RATE_LIMITED", run.ErrorMessage);
    }

    // ---------- Re-analysis (DEFINE step 5b) ----------

    [Fact]
    public async Task Reanalysis_SendsTheCurrentStateAndTheDevelopersInstruction()
    {
        var aiFeature = TestData.Feature("Đăng nhập");
        var definition = TestData.Definition(aiFeature);
        definition.LockedPersona = new() { PrimaryPersona = "Bệnh nhân cũ", Reason = "r" };
        var repository = Repository(definition, analyzedBefore: true);
        AiAnswers(Output(Item(Id(aiFeature), "Đăng nhập")));

        await Analyze(repository, new AnalyzeProductDefinitionRequest { Instruction = "bỏ thanh toán", ReassessPersona = true });

        var input = JsonSerializer.Deserialize<DefineAnalysisInput>(_lastRequest!.UserPrompt, AiJson.Options)!;
        var existing = Assert.Single(input.ExistingFeatures);
        Assert.True(existing.CanRegenerate);
        Assert.Equal("Bệnh nhân cũ", input.CurrentPersona!.PrimaryPersona);
        Assert.Equal("bỏ thanh toán", input.Instruction);
        Assert.True(input.ReassessPersona);
    }

    [Fact]
    public async Task Reanalysis_RegeneratesUntouchedAiFeaturesInPlace_AndDropsTheOnesLeftOut()
    {
        var regenerated = TestData.Feature("Đăng nhập", position: 0);
        var leftOut = TestData.Feature("Chat", position: 1);
        var definition = TestData.Definition(regenerated, leftOut);
        var repository = Repository(definition, analyzedBefore: true);
        AiAnswers(Output(Item(Id(regenerated), "Đăng nhập bằng Google")));

        await Analyze(repository);

        var only = Assert.Single(definition.Features);
        Assert.Same(regenerated, only);
        Assert.Equal("Đăng nhập bằng Google", regenerated.Name);
        Assert.Contains(leftOut, repository.RemovedFeatures);
    }

    [Fact]
    public async Task Reanalysis_LinkToADroppedFeatureIsRemovedAndReported()
    {
        var dropped = TestData.Feature("Thanh toán");
        var kept = TestData.Feature("Nhắc lịch", FeatureOrigin.FromCustomerMentioned, FeatureStatus.Included);
        TestData.DependsOn(kept, dropped);
        var definition = TestData.Definition(dropped, kept);
        var repository = Repository(definition, analyzedBefore: true);
        AiAnswers(Output(Item(Id(kept), "Nhắc lịch", FeatureOrigin.FromCustomerMentioned)));

        var response = await Analyze(repository);

        var removed = Assert.Single(response.RemovedDependencies);
        Assert.Equal(kept.Id, removed.FeatureId);
        Assert.Equal("Thanh toán", removed.RemovedPrerequisiteName);
        Assert.Empty(kept.Dependencies);
    }

    [Fact]
    public async Task Reanalysis_DecidedFeature_OnlyGetsANewAssessment()
    {
        var decided = TestData.Feature("Đăng nhập", status: FeatureStatus.Excluded, verdict: AiVerdict.Required);
        decided.DevDecided = true;
        var definition = TestData.Definition(decided);
        var repository = Repository(definition, analyzedBefore: true);
        var item = Item(Id(decided), "Tên khác", role: FeatureRole.Supporting, verdict: AiVerdict.NotRequired);
        AiAnswers(Output(item, Item("p1", "Trang chủ", dependsOn: Id(decided))));

        await Analyze(repository);

        Assert.Equal("Đăng nhập", decided.Name);
        Assert.Equal(FeatureStatus.Excluded, decided.Status);
        Assert.Equal(FeatureRole.Primary, decided.Role);
        Assert.Equal(AiVerdict.NotRequired, decided.AiVerdict);
        Assert.Equal("AI reason", decided.AiReason);
    }

    [Fact]
    public async Task Reanalysis_DecidedCommittedFeatureNeverAssessed_StillGetsRoleAndPersonaCheck()
    {
        var addedLater = TestData.Feature("Hồ sơ bệnh án", FeatureOrigin.FromCommittedList, FeatureStatus.Included, verdict: null);
        addedLater.Role = null;
        addedLater.DevDecided = true;
        var definition = TestData.Definition(addedLater);
        var repository = Repository(definition, analyzedBefore: true);
        var item = Item(Id(addedLater), "x", FeatureOrigin.FromCommittedList);
        item.PersonaConflict = true;
        item.PersonaConflictReason = "phục vụ bác sĩ";
        AiAnswers(Output(item));

        await Analyze(repository);

        Assert.Equal(FeatureRole.Primary, addedLater.Role);
        Assert.True(addedLater.PersonaConflict);
        Assert.Equal(FeatureStatus.Included, addedLater.Status);
    }

    [Fact]
    public async Task Reanalysis_KeptFeatureLeftOutByTheAi_Stays()
    {
        var kept = TestData.Feature("Thanh toán", FeatureOrigin.FromCustomerMentioned, FeatureStatus.Included);
        var aiFeature = TestData.Feature("Đăng nhập");
        var definition = TestData.Definition(kept, aiFeature);
        var repository = Repository(definition, analyzedBefore: true);
        AiAnswers(Output(Item(Id(aiFeature), "Đăng nhập")));

        await Analyze(repository);

        Assert.Contains(kept, definition.Features);
        Assert.DoesNotContain(kept, repository.RemovedFeatures);
    }

    [Fact]
    public async Task Reanalysis_LinksBetweenKeptFeaturesStayAsTheDeveloperLeftThem()
    {
        var booking = TestData.Feature("Đặt lịch", FeatureOrigin.FromCustomerMentioned, FeatureStatus.Included);
        var schedule = TestData.Feature("Lịch bác sĩ", FeatureOrigin.FromCustomerMentioned, FeatureStatus.Included);
        TestData.DependsOn(booking, schedule);
        var definition = TestData.Definition(booking, schedule);
        var repository = Repository(definition, analyzedBefore: true);
        // The AI drops the developer's link and proposes the reverse one; neither is applied.
        AiAnswers(Output(
            Item(Id(booking), "Đặt lịch", FeatureOrigin.FromCustomerMentioned),
            Item(Id(schedule), "Lịch bác sĩ", FeatureOrigin.FromCustomerMentioned, dependsOn: Id(booking))));

        await Analyze(repository);

        var link = Assert.Single(booking.Dependencies);
        Assert.Equal(schedule.Id, link.DependsOnFeatureId);
        Assert.Empty(schedule.Dependencies);
        Assert.Empty(repository.AddedDependencies);
        Assert.Empty(repository.RemovedDependencies);
    }

    [Fact]
    public async Task Reanalysis_ReadyDefinitionGoesBackToDraft()
    {
        var aiFeature = TestData.Feature("Đăng nhập");
        var definition = TestData.Definition(aiFeature);
        definition.Status = ProductDefinitionStatus.ReadyForLock;
        var repository = Repository(definition, analyzedBefore: true);
        AiAnswers(Output(Item(Id(aiFeature), "Đăng nhập")));

        await Analyze(repository);

        Assert.Equal(ProductDefinitionStatus.Draft, definition.Status);
    }

    /// <summary>Keeps features and links in memory the way EF tracks them, and records what was added or removed.</summary>
    private sealed class InMemoryDefinitions : IProductDefinitionRepository
    {
        private readonly ProductDefinition _definition;
        private readonly bool _analyzedBefore;

        public InMemoryDefinitions(ProductDefinition definition, bool analyzedBefore)
        {
            _definition = definition;
            _analyzedBefore = analyzedBefore;
        }

        public List<AiAnalysisRun> Runs { get; } = new();
        public List<Feature> RemovedFeatures { get; } = new();
        public List<FeatureDependency> AddedDependencies { get; } = new();
        public List<FeatureDependency> RemovedDependencies { get; } = new();
        public int Saves { get; private set; }

        public Task<ProductDefinition?> GetByWorkspaceIdAsync(Guid workspaceId) => Task.FromResult<ProductDefinition?>(_definition);
        public Task<bool> HasSuccessfulAnalysisRunAsync(Guid productDefinitionId) => Task.FromResult(_analyzedBefore);
        public Task AddAsync(ProductDefinition productDefinition) => Task.CompletedTask;

        public Task AddFeatureAsync(Feature feature)
        {
            _definition.Features.Add(feature);
            return Task.CompletedTask;
        }

        public void RemoveFeature(Feature feature)
        {
            RemovedFeatures.Add(feature);
            _definition.Features.Remove(feature);
        }

        public Task AddDependencyAsync(FeatureDependency dependency)
        {
            AddedDependencies.Add(dependency);
            _definition.Features.Single(f => f.Id == dependency.FeatureId).Dependencies.Add(dependency);
            return Task.CompletedTask;
        }

        public void RemoveDependency(FeatureDependency dependency)
        {
            RemovedDependencies.Add(dependency);
            _definition.Features.Single(f => f.Id == dependency.FeatureId).Dependencies.Remove(dependency);
        }

        public Task AddAnalysisRunAsync(AiAnalysisRun run)
        {
            Runs.Add(run);
            return Task.CompletedTask;
        }

        public Task AddChangeLogAsync(FeatureChangeLog changeLog) => Task.CompletedTask;
        public Task AddSnapshotAsync(MvpSnapshot snapshot) => Task.CompletedTask;
        public Task<MvpSnapshot?> GetLatestSnapshotAsync(Guid workspaceId) => Task.FromResult<MvpSnapshot?>(null);
        public Task<List<FeatureChangeLog>> GetChangeLogsAsync(Guid featureId) => Task.FromResult(new List<FeatureChangeLog>());

        public Task<bool> SaveChangesAsync()
        {
            Saves++;
            return Task.FromResult(true);
        }
    }
}
