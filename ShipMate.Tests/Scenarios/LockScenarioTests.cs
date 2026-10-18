using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Application.DTOs.Workspaces;
using ShipMate.Application.Exceptions;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests.Scenarios;

// Câu chuyện chung: app "đặt lịch khám" cho phòng khám.
//   Đăng nhập; Bác sĩ tạo lịch khám; Xem lịch bác sĩ (cần lịch do bác sĩ tạo); Đặt lịch khám (cần xem lịch).
// Mỗi test dựng một workspace trong bộ nhớ và đi qua các bước như người dùng thật. AI được giả, không tốn token.

public class SoloDevLockScenarios
{
    private const string Login = "Đăng nhập";
    private const string DoctorSlots = "Bác sĩ tạo lịch khám";
    private const string ViewSlots = "Xem lịch bác sĩ";
    private const string Book = "Đặt lịch khám";

    // Minh là Manager kiêm dev duy nhất. Budget = 10 x 1 = 10.
    private static (LockWorld World, Dictionary<string, ShipMate.Domain.Entities.Feature> F) Clinic(
        int? skill, int? login, int? slots, int? view, int? book)
    {
        var world = new LockWorld();
        world.AddMember("Minh", WorkspaceMemberRole.Manager, skill);
        var f = new Dictionary<string, ShipMate.Domain.Entities.Feature>();
        f[Login] = world.AddFeature(Login, login);
        f[DoctorSlots] = world.AddFeature(DoctorSlots, slots);
        f[ViewSlots] = world.AddFeature(ViewSlots, view, dependsOn: f[DoctorSlots]);
        f[Book] = world.AddFeature(Book, book, dependsOn: f[ViewSlots]);
        return (world, f);
    }

    [Fact(DisplayName = "A1 Solo dev: AI chấm điểm -> Manager xác nhận -> Lock thành công, tạo snapshot v1")]
    public async Task A1_FullHappyPath()
    {
        // Given: 4 feature chưa chấm điểm, Minh khai skill 4
        var (world, _) = Clinic(skill: 4, null, null, null, null);
        world.Ai.ScoreByName[Login] = 2;
        world.Ai.ScoreByName[DoctorSlots] = 3;
        world.Ai.ScoreByName[ViewSlots] = 2;
        world.Ai.ScoreByName[Book] = 3;

        // When: chưa chấm điểm thì kiểm tra Lock báo thiếu điểm
        var before = await world.Lock.CheckAsync(world.ManagerId, world.WorkspaceId);
        Assert.False(before.IsPassed);
        Assert.Equal(4, before.UnscoredFeatures.Count);

        // When: AI đề xuất điểm, Manager xác nhận đúng như đề xuất
        await world.Complexity.ProposeAsync(world.ManagerId, world.WorkspaceId);
        var proposals = world.Definition.Features.Select(f => new FeatureComplexityItem
        {
            FeatureId = f.Id,
            Complexity = f.ProposedComplexity!.Value
        }).ToList();
        await world.Complexity.SetAsync(world.ManagerId, world.WorkspaceId, new SetComplexityRequest { Items = proposals });

        // Then: qua kiểm tra (tổng 10 = budget 10), Lock tạo snapshot v1
        var gate = await world.Lock.CheckAsync(world.ManagerId, world.WorkspaceId);
        Assert.True(gate.IsPassed);
        Assert.Equal(10, gate.TotalComplexity);
        Assert.Equal(10, gate.Budget);

        await world.LockNow();

        var snapshot = world.LatestSnapshot;
        Assert.Equal(1, snapshot.Version);
        Assert.Equal(4, snapshot.Features.Count);
        Assert.Equal(2, snapshot.Dependencies.Count);
        Assert.Equal(1, snapshot.DevCountAtLock);
        Assert.Equal(64, snapshot.Hash.Length);
        Assert.Equal(ProductDefinitionStatus.Locked, world.Definition.Status);
        Assert.NotNull(world.Workspace.FirstLockedAt);
        Assert.Equal(1, world.Ai.Calls);
    }

    [Fact(DisplayName = "A2 Tổng điểm 12 vượt budget 10 -> chặn Lock; hạ điểm xuống 10 -> Lock được")]
    public async Task A2_BudgetExceeded_ThenFixed()
    {
        var (world, f) = Clinic(skill: 4, login: 3, slots: 3, view: 3, book: 3);

        var ex = await Assert.ThrowsAsync<LockGateFailedException>(() => world.LockNow());

        Assert.True(ex.Gate.BudgetExceeded);
        Assert.Equal(12, ex.Gate.TotalComplexity);
        Assert.Equal(10, ex.Gate.Budget);
        Assert.Empty(world.Definitions.Snapshots);
        Assert.Equal(ProductDefinitionStatus.ReadyForLock, world.Definition.Status);

        await world.Complexity.SetAsync(world.ManagerId, world.WorkspaceId, new SetComplexityRequest
        {
            Items =
            [
                new FeatureComplexityItem { FeatureId = f[Login].Id, Complexity = 2 },
                new FeatureComplexityItem { FeatureId = f[ViewSlots].Id, Complexity = 2 }
            ]
        });
        await world.LockNow();

        Assert.Single(world.Definitions.Snapshots);
    }

    [Fact(DisplayName = "A3 Feature đã chốt khó 5 mà skill dev chỉ 4 -> chặn và gắn cờ 'đã chốt'; nâng skill lên 5 -> Lock được")]
    public async Task A3_SkillCeiling_CommittedFeatureIsFlagged()
    {
        var world = new LockWorld();
        world.AddMember("Minh", WorkspaceMemberRole.Manager, skill: 4);
        var vnpay = world.AddFeature("Thanh toán VNPay", 5, FeatureOrigin.FromCommittedList);
        world.AddFeature(Login, 1);

        var ex = await Assert.ThrowsAsync<LockGateFailedException>(() => world.LockNow());

        var violation = Assert.Single(ex.Gate.SkillCeilingViolations);
        Assert.Equal(vnpay.Id, violation.FeatureId);
        Assert.True(violation.IsCommitted); // frontend không được gợi ý đẩy sang Kill List

        await world.Workspaces.UpdateMySkillAsync(world.ManagerId, world.WorkspaceId, new UpdateMySkillRequest { SkillLevel = 5 });
        await world.LockNow();

        Assert.Single(world.Definitions.Snapshots);
    }

    [Fact(DisplayName = "A4 Dev chưa khai skill -> chặn Lock; khai skill xong -> Lock được")]
    public async Task A4_SkillNotDeclared()
    {
        var (world, _) = Clinic(skill: null, login: 2, slots: 2, view: 2, book: 2);

        var gate = await world.Lock.CheckAsync(world.ManagerId, world.WorkspaceId);
        Assert.Equal(1, gate.MembersWithoutSkill);
        await Assert.ThrowsAsync<LockGateFailedException>(() => world.LockNow());

        await world.Workspaces.UpdateMySkillAsync(world.ManagerId, world.WorkspaceId, new UpdateMySkillRequest { SkillLevel = 3 });
        await world.LockNow();

        Assert.Single(world.Definitions.Snapshots);
    }

    [Fact(DisplayName = "A5 Sau khi Lock, DEFINE đóng băng: sửa feature, ràng buộc, chạy AI, sửa Vision Prompt đều bị từ chối")]
    public async Task A5_DefineIsFrozenAfterLock()
    {
        var (world, f) = Clinic(skill: 4, login: 2, slots: 2, view: 2, book: 2);
        await world.LockNow();
        var visionBefore = world.Workspace.VisionPrompt;

        await Assert.ThrowsAsync<ProductDefinitionLockedException>(() =>
            world.Review.EditAsync(world.ManagerId, world.WorkspaceId, f[Login].Id, new UpdateFeatureRequest { Name = "Đổi tên" }));
        await Assert.ThrowsAsync<ProductDefinitionLockedException>(() =>
            world.DefinitionService.UpdateConstraintsAsync(world.ManagerId, world.WorkspaceId, new UpdateProjectConstraintsRequest { Team = "x" }));
        await Assert.ThrowsAsync<ProductDefinitionLockedException>(() =>
            world.Analysis.AnalyzeAsync(world.ManagerId, world.WorkspaceId, null));
        await Assert.ThrowsAsync<ProductDefinitionLockedException>(() =>
            world.Workspaces.UpdateAsync(world.ManagerId, world.WorkspaceId, new UpdateWorkspaceRequest { VisionPrompt = "Ý tưởng khác" }));
        await Assert.ThrowsAsync<ProductDefinitionLockedException>(() =>
            world.Complexity.ProposeAsync(world.ManagerId, world.WorkspaceId));

        Assert.Equal(visionBefore, world.Workspace.VisionPrompt);
        Assert.Equal(Login, f[Login].Name);
        Assert.Equal(0, world.Ai.Calls);
    }

    [Fact(DisplayName = "A6 Lock lần 2 bị từ chối, không tạo snapshot thứ hai, FirstLockedAt không đổi")]
    public async Task A6_LockTwice()
    {
        var (world, _) = Clinic(skill: 4, login: 2, slots: 2, view: 2, book: 2);
        await world.LockNow();
        var firstLockedAt = world.Workspace.FirstLockedAt;

        await Assert.ThrowsAsync<ProductDefinitionLockedException>(() => world.LockNow());

        Assert.Single(world.Definitions.Snapshots);
        Assert.Equal(firstLockedAt, world.Workspace.FirstLockedAt);
    }

    [Fact(DisplayName = "A8 Chấm điểm xong rồi sửa mô tả feature -> điểm bị xóa, Lock bị chặn tới khi chấm lại")]
    public async Task A8_EditingAScoredFeatureRequiresRescoring()
    {
        var (world, f) = Clinic(skill: 4, login: 2, slots: 2, view: 2, book: 2);

        await world.Review.EditAsync(world.ManagerId, world.WorkspaceId, f[Book].Id,
            new UpdateFeatureRequest { Description = "Đặt lịch khám, kèm thanh toán trước" });
        await world.DefinitionService.MarkReadyForLockAsync(world.ManagerId, world.WorkspaceId);

        var ex = await Assert.ThrowsAsync<LockGateFailedException>(() => world.LockNow());
        Assert.Equal(Book, Assert.Single(ex.Gate.UnscoredFeatures).Name);

        await world.Complexity.SetAsync(world.ManagerId, world.WorkspaceId, new SetComplexityRequest
        {
            Items = [new FeatureComplexityItem { FeatureId = f[Book].Id, Complexity = 4 }]
        });
        await world.LockNow();

        Assert.Equal(4, world.InSnapshot(world.LatestSnapshot, Book).Complexity);
    }

    [Fact(DisplayName = "A7 Product Definition còn ở Draft (chưa READY_FOR_LOCK) -> không Lock được")]
    public async Task A7_DraftCannotLock()
    {
        var (world, _) = Clinic(skill: 4, login: 2, slots: 2, view: 2, book: 2);
        world.Definition.Status = ProductDefinitionStatus.Draft;

        await Assert.ThrowsAsync<ProductDefinitionNotReadyForLockException>(() => world.LockNow());

        Assert.Empty(world.Definitions.Snapshots);
    }
}

public class TeamLockAndAssignmentScenarios
{
    // Team 3 người: Minh (Manager, skill 4), An (skill 5), Bình (skill 2). Budget = 10 x 3 = 30.
    private static async Task<(LockWorld World, ShipMate.Domain.Entities.WorkspaceMember Minh, ShipMate.Domain.Entities.WorkspaceMember An, ShipMate.Domain.Entities.WorkspaceMember Binh, ShipMate.Domain.Entities.Feature Book, ShipMate.Domain.Entities.Feature Login)> LockedTeam()
    {
        var world = new LockWorld();
        var minh = world.AddMember("Minh", WorkspaceMemberRole.Manager, 4);
        var an = world.AddMember("An", WorkspaceMemberRole.Developer, 5);
        var binh = world.AddMember("Bình", WorkspaceMemberRole.Developer, 2, github: "binh-dev");
        var login = world.AddFeature("Đăng nhập", 2);
        var slots = world.AddFeature("Bác sĩ tạo lịch khám", 3);
        var view = world.AddFeature("Xem lịch bác sĩ", 4, dependsOn: slots);
        var book = world.AddFeature("Đặt lịch khám", 5, dependsOn: view);
        await world.LockNow();
        return (world, minh, an, binh, book, login);
    }

    [Fact(DisplayName = "B1 Team 3 người: feature khó 5 vẫn Lock được vì An có skill 5; budget 30, DevCount = 3")]
    public async Task B1_TeamLock()
    {
        var (world, _, _, _, _, _) = await LockedTeam();

        var snapshot = world.LatestSnapshot;
        Assert.Equal(3, snapshot.DevCountAtLock);
        Assert.Equal(4, snapshot.Features.Count);
        Assert.Equal(14, snapshot.Features.Sum(f => f.Complexity));
    }

    [Fact(DisplayName = "B2 Danh sách nhận feature: chỉ những dev đủ skill được liệt kê cho từng feature")]
    public async Task B2_QualifiedMembersPerFeature()
    {
        var (world, minh, an, _, book, login) = await LockedTeam();

        var overview = await world.Assignment.GetOverviewAsync(minh.UserId, world.WorkspaceId);

        var hard = overview.Single(o => o.FeatureId == book.Id);
        Assert.Equal([an.UserId], hard.QualifiedMembers.Select(m => m.UserId));
        var easy = overview.Single(o => o.FeatureId == login.Id);
        Assert.Equal(3, easy.QualifiedMembers.Count);
    }

    [Fact(DisplayName = "B3 An (skill 5) nhận feature khó và được sinh tên nhánh; Bình (skill 2) nhận feature khó bị từ chối nhưng nhận feature dễ được")]
    public async Task B3_ClaimBySkill()
    {
        var (world, _, an, binh, book, login) = await LockedTeam();

        await Assert.ThrowsAsync<SkillTooLowForFeatureException>(() =>
            world.Assignment.ClaimAsync(binh.UserId, world.WorkspaceId, book.Id));

        var anResult = await world.Assignment.ClaimAsync(an.UserId, world.WorkspaceId, book.Id);
        Assert.Equal($"feature/{book.Id}-an", anResult.BranchName);

        var binhResult = await world.Assignment.ClaimAsync(binh.UserId, world.WorkspaceId, login.Id);
        Assert.Equal($"feature/{login.Id}-binh-dev", binhResult.BranchName);
    }

    [Fact(DisplayName = "B4 Hai dev cùng nhận một feature: người bấm trước được, người sau nhận lỗi FEATURE_ALREADY_ASSIGNED")]
    public async Task B4_FirstComeFirstServed()
    {
        var (world, minh, an, _, _, login) = await LockedTeam();

        await world.Assignment.ClaimAsync(an.UserId, world.WorkspaceId, login.Id);
        await Assert.ThrowsAsync<FeatureAlreadyAssignedException>(() =>
            world.Assignment.ClaimAsync(minh.UserId, world.WorkspaceId, login.Id));

        var overview = await world.Assignment.GetOverviewAsync(minh.UserId, world.WorkspaceId);
        Assert.Equal("An", overview.Single(o => o.FeatureId == login.Id).Assignee!.DisplayName);
    }

    [Fact(DisplayName = "B5 Dev chưa kết nối GitHub không nhận được feature (không có username để đặt tên nhánh)")]
    public async Task B5_NoGitHub()
    {
        var (world, _, _, _, _, login) = await LockedTeam();
        var cuong = world.AddMember("Cường", WorkspaceMemberRole.Developer, 3, github: null);

        await Assert.ThrowsAsync<GitHubNotConnectedException>(() =>
            world.Assignment.ClaimAsync(cuong.UserId, world.WorkspaceId, login.Id));
    }
}

public class FeatureChallengeScenarios
{
    // Minh (Manager, skill 5) làm solo, budget 10. Snapshot v1: Đăng nhập 1, Bác sĩ tạo lịch 3, Xem lịch 2 (cần tạo lịch),
    // Thanh toán VNPay 1 (đã chốt với khách). Tổng 7.
    private static async Task<LockWorld> LockedSolo(int skill = 5)
    {
        var world = new LockWorld();
        world.AddMember("Minh", WorkspaceMemberRole.Manager, skill);
        world.AddFeature("Đăng nhập", 1);
        var slots = world.AddFeature("Bác sĩ tạo lịch khám", 3);
        world.AddFeature("Xem lịch bác sĩ", 2, dependsOn: slots);
        world.AddFeature("Thanh toán VNPay", 1, FeatureOrigin.FromCommittedList);
        await world.LockNow();
        return world;
    }

    private static Guid IdOf(LockWorld world, string name) => world.InSnapshot(world.LatestSnapshot, name).FeatureId;

    [Fact(DisplayName = "C1 Thêm thẳng feature vừa budget: AI đề xuất 3, gợi ý AddDirectly, duyệt -> snapshot v2, v1 giữ nguyên")]
    public async Task C1_AddDirectly()
    {
        var world = await LockedSolo();
        var v1 = world.LatestSnapshot;
        var v1Hash = v1.Hash;

        var challenge = await world.SubmitChallenge(world.Manager, "Nhắc lịch khám", aiScore: 3, confirmedComplexity: 3);
        var detail = await world.Challenge.GetAsync(world.ManagerId, world.WorkspaceId, challenge.Id);
        Assert.Equal(ChallengeAction.AddDirectly, detail.Recommendation!.Action);

        await world.Challenge.ApproveAsync(world.ManagerId, world.WorkspaceId, challenge.Id,
            new ApproveChallengeRequest { Action = ChallengeAction.AddDirectly });

        var v2 = world.LatestSnapshot;
        Assert.Equal(2, v2.Version);
        Assert.Equal(5, v2.Features.Count);
        var added = world.InSnapshot(v2, "Nhắc lịch khám");
        Assert.Equal(FeatureOrigin.FromAi, added.Origin);
        Assert.Equal(SnapshotFeatureStatus.Locked, added.Status);

        Assert.Equal(4, v1.Features.Count);
        Assert.Equal(v1Hash, v1.Hash);
        Assert.NotEqual(v1Hash, v2.Hash);
    }

    [Fact(DisplayName = "C2 Hết budget: gợi ý bỏ feature từ AI có điểm thấp nhất đủ nhường chỗ, KHÔNG gợi ý feature đã chốt")]
    public async Task C2_RecommendsReplacingTheRightFeature()
    {
        var world = await LockedSolo();
        await world.SubmitChallenge(world.Manager, "Nhắc lịch khám", 3, 3);
        var first = world.Challenges.GetByWorkspaceIdAsync(world.WorkspaceId).Result.First();
        await world.Challenge.ApproveAsync(world.ManagerId, world.WorkspaceId, first.Id,
            new ApproveChallengeRequest { Action = ChallengeAction.AddDirectly }); // tổng = 10, đầy budget

        var second = await world.SubmitChallenge(world.Manager, "Báo cáo doanh thu", aiScore: 2, confirmedComplexity: 2);
        var detail = await world.Challenge.GetAsync(world.ManagerId, world.WorkspaceId, second.Id);

        Assert.Equal(ChallengeAction.RemoveAndReplace, detail.Recommendation!.Action);
        var candidate = detail.Recommendation.FeatureToRemove!;
        Assert.Equal("Xem lịch bác sĩ", candidate.Name); // điểm 2 là thấp nhất đủ chỗ, không phải VNPay (đã chốt, điểm 1 cũng không đủ chỗ)
        Assert.False(candidate.IsCommitted);
    }

    [Fact(DisplayName = "C3a Bỏ feature có feature phụ thuộc: bắt buộc chọn; chọn 'tái cấu trúc' -> feature phụ thuộc chuyển sang feature mới")]
    public async Task C3a_RemoveWithDependents_Restructure()
    {
        var world = await LockedSolo();
        var challenge = await world.SubmitChallenge(world.Manager, "Lịch khám tự động", 3, 3);
        var slotsId = IdOf(world, "Bác sĩ tạo lịch khám");
        var request = new ApproveChallengeRequest { Action = ChallengeAction.RemoveAndReplace, RemoveFeatureId = slotsId };

        var ex = await Assert.ThrowsAsync<DependentsResolutionRequiredException>(() =>
            world.Challenge.ApproveAsync(world.ManagerId, world.WorkspaceId, challenge.Id, request));
        Assert.Equal("Xem lịch bác sĩ", Assert.Single(ex.AffectedDependents).Name);
        Assert.Single(world.Definitions.Snapshots);

        request.DependentsResolution = RemovalDependentsResolution.Restructure;
        await world.Challenge.ApproveAsync(world.ManagerId, world.WorkspaceId, challenge.Id, request);

        var v2 = world.LatestSnapshot;
        var replacement = world.InSnapshot(v2, "Lịch khám tự động");
        var view = world.InSnapshot(v2, "Xem lịch bác sĩ");
        Assert.Equal(SnapshotFeatureStatus.Replaced, world.InSnapshot(v2, "Bác sĩ tạo lịch khám").Status);
        Assert.Equal(SnapshotFeatureStatus.Locked, view.Status);
        Assert.Contains(v2.Dependencies, d => d.FeatureId == view.FeatureId && d.DependsOnFeatureId == replacement.FeatureId);
    }

    [Fact(DisplayName = "C3b Bỏ feature có feature phụ thuộc: chọn 'xóa luôn feature phụ thuộc' -> cả hai thành replaced")]
    public async Task C3b_RemoveWithDependents_RemoveAll()
    {
        var world = await LockedSolo();
        var challenge = await world.SubmitChallenge(world.Manager, "Lịch khám tự động", 3, 3);

        await world.Challenge.ApproveAsync(world.ManagerId, world.WorkspaceId, challenge.Id, new ApproveChallengeRequest
        {
            Action = ChallengeAction.RemoveAndReplace,
            RemoveFeatureId = IdOf(world, "Bác sĩ tạo lịch khám"),
            DependentsResolution = RemovalDependentsResolution.RemoveDependents
        });

        var v2 = world.LatestSnapshot;
        Assert.Equal(SnapshotFeatureStatus.Replaced, world.InSnapshot(v2, "Bác sĩ tạo lịch khám").Status);
        Assert.Equal(SnapshotFeatureStatus.Replaced, world.InSnapshot(v2, "Xem lịch bác sĩ").Status);
        Assert.Empty(v2.Dependencies);
    }

    [Fact(DisplayName = "C4 Bỏ feature đã chốt với khách (VNPay): phải tick xác nhận mới được duyệt")]
    public async Task C4_CommittedRemovalNeedsConfirmation()
    {
        var world = await LockedSolo();
        var challenge = await world.SubmitChallenge(world.Manager, "Thanh toán Momo", 1, 1);
        var request = new ApproveChallengeRequest
        {
            Action = ChallengeAction.RemoveAndReplace,
            RemoveFeatureId = IdOf(world, "Thanh toán VNPay")
        };

        await Assert.ThrowsAsync<CommittedRemovalNotConfirmedException>(() =>
            world.Challenge.ApproveAsync(world.ManagerId, world.WorkspaceId, challenge.Id, request));
        Assert.Single(world.Definitions.Snapshots);

        request.ConfirmCommittedRemoval = true;
        await world.Challenge.ApproveAsync(world.ManagerId, world.WorkspaceId, challenge.Id, request);

        Assert.Equal(SnapshotFeatureStatus.Replaced, world.InSnapshot(world.LatestSnapshot, "Thanh toán VNPay").Status);
    }

    [Fact(DisplayName = "C5 Feature khó 5 mà dev cao nhất chỉ skill 3 -> gợi ý Backup; có dev skill 5 vào nhận -> thành locked trong CÙNG version")]
    public async Task C5_BackupThenClaimedByNewDeveloper()
    {
        var world = await LockedSolo(skill: 3);
        world.AddMember("Lan", WorkspaceMemberRole.Developer, 3); // thêm 1 dev nữa: budget 20, vẫn chưa ai đủ skill 5
        var challenge = await world.SubmitChallenge(world.Manager, "Tích hợp bảo hiểm y tế", aiScore: 5, confirmedComplexity: 5);
        var detail = await world.Challenge.GetAsync(world.ManagerId, world.WorkspaceId, challenge.Id);
        Assert.Equal(ChallengeAction.Backup, detail.Recommendation!.Action);

        await world.Challenge.ApproveAsync(world.ManagerId, world.WorkspaceId, challenge.Id,
            new ApproveChallengeRequest { Action = ChallengeAction.Backup });

        var v2 = world.LatestSnapshot;
        var backup = world.InSnapshot(v2, "Tích hợp bảo hiểm y tế");
        Assert.Equal(SnapshotFeatureStatus.Backup, backup.Status);
        var hashWhileBackup = v2.Hash;

        // Hùng (skill 5) vào workspace và nhận feature backup.
        var hung = world.AddMember("Hùng", WorkspaceMemberRole.Developer, 5);
        await world.Assignment.ClaimAsync(hung.UserId, world.WorkspaceId, backup.FeatureId);

        Assert.Equal(2, world.Definitions.Snapshots.Count); // không tạo version mới
        Assert.Equal(SnapshotFeatureStatus.Locked, backup.Status);
        Assert.NotEqual(hashWhileBackup, v2.Hash);
    }

    [Fact(DisplayName = "C6 Manager từ chối challenge: snapshot không đổi, challenge ghi lý do, không duyệt lại được")]
    public async Task C6_Reject()
    {
        var world = await LockedSolo();
        var challenge = await world.SubmitChallenge(world.Manager, "Chat với bác sĩ", 4, 4);

        await world.Challenge.RejectAsync(world.ManagerId, world.WorkspaceId, challenge.Id,
            new RejectChallengeRequest { Reason = "Ngoài phạm vi MVP" });

        Assert.Single(world.Definitions.Snapshots);
        var saved = await world.Challenge.GetAsync(world.ManagerId, world.WorkspaceId, challenge.Id);
        Assert.Equal(ChallengeStatus.Rejected, saved.Status);
        Assert.Equal("Ngoài phạm vi MVP", saved.RejectionReason);
        await Assert.ThrowsAsync<ChallengeAlreadyDecidedException>(() =>
            world.Challenge.ApproveAsync(world.ManagerId, world.WorkspaceId, challenge.Id,
                new ApproveChallengeRequest { Action = ChallengeAction.AddDirectly }));
    }

    [Fact(DisplayName = "C7 Dev thường gửi challenge được và chỉnh điểm của mình, nhưng không được duyệt; Manager mới duyệt")]
    public async Task C7_DeveloperCannotApprove()
    {
        var world = await LockedSolo();
        var an = world.AddMember("An", WorkspaceMemberRole.Developer, 4);

        var challenge = await world.SubmitChallenge(an, "Xuất báo cáo PDF", aiScore: 5, confirmedComplexity: 2);
        Assert.Equal(5, challenge.ProposedComplexity);

        await Assert.ThrowsAsync<WorkspacePermissionDeniedException>(() =>
            world.Challenge.ApproveAsync(an.UserId, world.WorkspaceId, challenge.Id,
                new ApproveChallengeRequest { Action = ChallengeAction.AddDirectly }));

        await world.Challenge.ApproveAsync(world.ManagerId, world.WorkspaceId, challenge.Id,
            new ApproveChallengeRequest { Action = ChallengeAction.AddDirectly });
        Assert.Equal(2, world.LatestSnapshot.Version);
    }

    [Fact(DisplayName = "C8 Duyệt thêm thẳng nhưng vượt budget -> cổng kiểm tra chặn, challenge vẫn Open, không có snapshot mới")]
    public async Task C8_GateBlocksApproval()
    {
        var world = await LockedSolo();
        var challenge = await world.SubmitChallenge(world.Manager, "Đặt lịch qua Zalo", aiScore: 5, confirmedComplexity: 5);

        var ex = await Assert.ThrowsAsync<LockGateFailedException>(() =>
            world.Challenge.ApproveAsync(world.ManagerId, world.WorkspaceId, challenge.Id,
                new ApproveChallengeRequest { Action = ChallengeAction.AddDirectly }));

        Assert.True(ex.Gate.BudgetExceeded);
        Assert.Single(world.Definitions.Snapshots);
        var saved = await world.Challenge.GetAsync(world.ManagerId, world.WorkspaceId, challenge.Id);
        Assert.Equal(ChallengeStatus.Open, saved.Status);
    }

    [Fact(DisplayName = "C10 Bỏ feature đã có người nhận: Manager thấy tên người nhận trước khi duyệt; duyệt xong người đó được giải phóng")]
    public async Task C10_RemovingAnAssignedFeatureShowsAndReleasesTheAssignee()
    {
        var world = await LockedSolo();
        var an = world.AddMember("An", WorkspaceMemberRole.Developer, 4);
        var binh = world.AddMember("Bình", WorkspaceMemberRole.Developer, 3); // budget 30
        var slotsId = IdOf(world, "Bác sĩ tạo lịch khám");
        var viewId = IdOf(world, "Xem lịch bác sĩ");
        await world.Assignment.ClaimAsync(an.UserId, world.WorkspaceId, slotsId);
        await world.Assignment.ClaimAsync(binh.UserId, world.WorkspaceId, viewId);

        var challenge = await world.SubmitChallenge(world.Manager, "Lịch khám tự động", 3, 3);
        var request = new ApproveChallengeRequest { Action = ChallengeAction.RemoveAndReplace, RemoveFeatureId = slotsId };

        // Trước khi chọn cách xử lý: lỗi liệt kê feature phụ thuộc KÈM người đang nhận
        var ex = await Assert.ThrowsAsync<DependentsResolutionRequiredException>(() =>
            world.Challenge.ApproveAsync(world.ManagerId, world.WorkspaceId, challenge.Id, request));
        var affected = Assert.Single(ex.AffectedDependents);
        Assert.Equal("Xem lịch bác sĩ", affected.Name);
        Assert.Equal("Bình", affected.AssignedTo);

        request.DependentsResolution = RemovalDependentsResolution.RemoveDependents;
        await world.Challenge.ApproveAsync(world.ManagerId, world.WorkspaceId, challenge.Id, request);

        // Cả An và Bình được giải phóng; hàng assignment vẫn còn để làm lịch sử
        var all = await world.Assignments.GetByWorkspaceIdAsync(world.WorkspaceId);
        Assert.Equal(2, all.Count);
        Assert.All(all, a => Assert.NotNull(a.ReleasedAt));
        var overview = await world.Assignment.GetOverviewAsync(world.ManagerId, world.WorkspaceId);
        Assert.DoesNotContain(overview, o => o.FeatureId == slotsId || o.FeatureId == viewId);
    }

    [Fact(DisplayName = "C11 Gợi ý thay thế hiện tên người đang nhận feature bị đề xuất bỏ")]
    public async Task C11_RecommendationShowsTheAssignee()
    {
        var world = await LockedSolo();
        await world.SubmitChallenge(world.Manager, "Nhắc lịch khám", 3, 3);
        var first = (await world.Challenges.GetByWorkspaceIdAsync(world.WorkspaceId)).First();
        await world.Challenge.ApproveAsync(world.ManagerId, world.WorkspaceId, first.Id,
            new ApproveChallengeRequest { Action = ChallengeAction.AddDirectly }); // tổng = 10, đầy budget
        await world.Assignment.ClaimAsync(world.ManagerId, world.WorkspaceId, IdOf(world, "Xem lịch bác sĩ"));

        var second = await world.SubmitChallenge(world.Manager, "Báo cáo doanh thu", 2, 2);
        var detail = await world.Challenge.GetAsync(world.ManagerId, world.WorkspaceId, second.Id);

        Assert.Equal("Xem lịch bác sĩ", detail.Recommendation!.FeatureToRemove!.Name);
        Assert.Equal("Minh", detail.Recommendation.FeatureToRemove.AssignedTo);
    }

    [Fact(DisplayName = "C12 Tái cấu trúc sang feature khác: AI kiểm tra lệch persona -> chỉ cảnh báo, lưu vào challenge, Manager vẫn duyệt được")]
    public async Task C12_PersonaCheckWarnsButDoesNotBlock()
    {
        var world = await LockedSolo();
        var challenge = await world.SubmitChallenge(world.Manager, "Lịch khám tự động", 3, 3);
        var loginId = IdOf(world, "Đăng nhập");
        world.Ai.PersonaAnswer = (true, "Đăng nhập không thay được việc bác sĩ tạo lịch");

        var check = await world.Challenge.CheckReplacementAsync(world.ManagerId, world.WorkspaceId, challenge.Id,
            new ReplacementCheckRequest { ReplacementFeatureId = loginId });

        Assert.True(check.PersonaConflict);
        Assert.Equal("Đăng nhập không thay được việc bác sĩ tạo lịch", check.Reason);
        var saved = (await world.Challenges.GetByIdAsync(world.WorkspaceId, challenge.Id))!;
        Assert.Equal(loginId, saved.PersonaCheckFeatureId);
        Assert.True(saved.PersonaConflict);

        await world.Challenge.ApproveAsync(world.ManagerId, world.WorkspaceId, challenge.Id, new ApproveChallengeRequest
        {
            Action = ChallengeAction.RemoveAndReplace,
            RemoveFeatureId = IdOf(world, "Bác sĩ tạo lịch khám"),
            DependentsResolution = RemovalDependentsResolution.Restructure,
            ReplacementFeatureId = loginId
        });
        Assert.Equal(2, world.LatestSnapshot.Version);
    }

    [Fact(DisplayName = "C13 Kiểm tra persona: feature thay thế không có trong snapshot -> từ chối; AI trả kết quả sai -> lỗi, không lưu gì")]
    public async Task C13_PersonaCheckRejectsBadInput()
    {
        var world = await LockedSolo();
        var challenge = await world.SubmitChallenge(world.Manager, "Lịch khám tự động", 3, 3);

        await Assert.ThrowsAsync<InvalidReplacementException>(() =>
            world.Challenge.CheckReplacementAsync(world.ManagerId, world.WorkspaceId, challenge.Id,
                new ReplacementCheckRequest { ReplacementFeatureId = Guid.NewGuid() }));

        world.Ai.PersonaAnswer = null;
        await Assert.ThrowsAsync<AiOutputInvalidException>(() =>
            world.Challenge.CheckReplacementAsync(world.ManagerId, world.WorkspaceId, challenge.Id,
                new ReplacementCheckRequest { ReplacementFeatureId = IdOf(world, "Đăng nhập") }));

        Assert.Null((await world.Challenges.GetByIdAsync(world.WorkspaceId, challenge.Id))!.PersonaConflict);
    }

    [Fact(DisplayName = "C9 Chưa Lock thì không gửi được challenge")]
    public async Task C9_NoChallengeBeforeFirstLock()
    {
        var world = new LockWorld();
        world.AddMember("Minh", WorkspaceMemberRole.Manager, 4);
        world.AddFeature("Đăng nhập", 2);

        await Assert.ThrowsAsync<NotFoundException>(() => world.SubmitChallenge(world.Manager, "Thêm gì đó", 2));
    }
}
