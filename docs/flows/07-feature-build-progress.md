# Tiến độ BUILD theo từng feature: kế hoạch cài đặt

**Trạng thái (2026-11-15): đã code xong, chưa commit.** 849/849 unit test đạt (thêm 21: 13 ca ở `FeatureBuildStatePolicyTests`, 8 ca ở `FeatureAssignmentTests`). Đã chạy thật trên DB dev qua HTTP: `GET /assignments` trả khối `build` cho 7 workspace đã Lock; hai truy vấn mới dịch đúng sang SQL (`ROW_NUMBER() OVER (PARTITION BY feature_id ORDER BY created_at DESC)` và `LEFT JOIN users`, không đọc `ai_raw_response`), không lỗi. **Chưa kiểm được trên dữ liệu thật** các trạng thái khác `NotSubmitted`, vì DB dev chưa có submission nào; các trạng thái này mới có unit test. Đã chốt theo đề xuất: P1 (đặt vào `/assignments`), P2 (không làm "đã vào `main`"), P3 (không thêm sự kiện lúc submit). Tài liệu cho frontend: `docs/frontend-guide.md` mục 2.11.

Khác kế hoạch ở hai chỗ nhỏ:
- Tên người duyệt lấy bằng `LEFT JOIN users` ngay trong truy vấn, không lấy từ danh sách thành viên. Lý do: `IWorkspaceMemberRepository.GetByWorkspaceIdAsync` bỏ qua thành viên `Removed`, nên nếu lấy từ đó thì người duyệt đã bị xóa khỏi workspace sẽ mất tên.
- Lần duyệt gần nhất của mỗi feature được chọn sau truy vấn (một feature chỉ được duyệt vài lần), còn truy vấn chỉ đọc các cột duyệt. Submission mới nhất thì vẫn chọn trong SQL.

Kế hoạch thêm vào `GET /api/workspaces/{id}/assignments` trạng thái BUILD của từng feature: đang làm, đang drift, chờ duyệt, đã duyệt; ai duyệt, lúc nào. Viết trước khi code, đối chiếu với code trên nhánh `feature/verify` (commit `0c6617a`).

> **Thứ tự làm theo [kế hoạch tổng thể](00-ke-hoach-tong-the.md):** đây là **đợt 3**, làm sau D2 (giải phóng feature khi xóa thành viên). D2 làm một feature có thể có nhiều dòng `feature_assignments` (dòng cũ đã `ReleasedAt`). Overview vẫn chỉ lấy dòng chưa giải phóng như hiện nay. Nhưng lịch sử submission của feature thì gồm cả các lần của người giữ cũ, nên `lastApproval.approvedByName` có thể là tên người giữ cũ. Đây là thông tin đúng: người đó đã duyệt phần code đang nằm trên nhánh.

## 1. Vấn đề

Hiện không có API nào trả về "feature nào đã làm xong và được duyệt" theo từng feature (đã kiểm tra cả 25 endpoint GET):

| API | Có gì | Thiếu gì |
|---|---|---|
| `GET /assignments` | Người nhận, tên nhánh, người đủ skill | Không có trạng thái BUILD nào |
| `GET /build/submissions/{id}` | `status`, `approvedAt`, `approvedSha` | Phải biết sẵn id; không có API danh sách |
| `GET /verify/checklists/{v}` → `info` | `approvedFeatures`, `lockedFeatures` | Chỉ là số đếm |
| `GET /next-best-action` | Một việc tiếp theo | Không phải danh sách |

Dữ liệu đã có trong `build_submissions` (`Status`, `SubmittedByUserId`, `ApprovedByUserId`, `ApprovedAt`, `ApprovedSha`) và `drift_alerts`. **Không cần migration**, chỉ thêm phần đọc dữ liệu.

## 2. Định nghĩa (phải khớp với NBA và VERIFY)

- **Submission mới nhất** của một feature: `build_submissions` cùng workspace và feature, sắp theo `created_at` giảm dần, lấy dòng đầu. Giống hệt `BuildSubmissionRepository.GetLatestAsync`, nên kết quả trùng với NBA và `info` của VERIFY.
- **Đã duyệt** = submission mới nhất có `Status = Approved`. Cùng định nghĩa với `VerificationService.InfoAsync` và `NextBestActionService.EveryLockedFeatureApprovedAsync`. Nghĩa là bot đã đẩy code sang nhánh `feature/...`; **không** có nghĩa là đã vào `main` (ShipMate không ghi lúc merge PR).
- **Chờ duyệt** = `BuildApprovalPolicy.Evaluate(newest, newest, hasOpenAlert, latestAlertStatus) == Ready`. Đây là đúng hàm mà nút Duyệt và NBA đang dùng, không viết lại luật.

### Trạng thái `state` (xét theo thứ tự, gặp điều kiện nào trước thì dừng)

| # | `state` | Điều kiện |
|---|---|---|
| 1 | `NotSubmitted` | Chưa có submission nào |
| 2 | `Approved` | Submission mới nhất là `Approved` |
| 3 | `Analyzing` | Submission mới nhất là `Analyzing` |
| 4 | `Failed` | Submission mới nhất là `Failed` (kèm `failureCode`) |
| 5 | `DriftUnresolved` | Có alert đang mở (`unresolved`/`acknowledged`) của feature, hoặc `Evaluate` trả `DriftUnresolved` |
| 6 | `ReadyToApprove` | `Evaluate` trả `Ready` |

Ghi chú:
- `Superseded` không bao giờ là submission mới nhất (nó bị thay bởi chính submission mới hơn), nên không có trạng thái riêng.
- `SkippedMaintenance` lặp lại kết luận của lần trước. `Evaluate` đã xử lý trường hợp này (cần alert gần nhất là `overridden` nếu kết luận cũ vượt scope), nên rơi vào 5 hoặc 6.
- Feature `Backup` chưa có người nhận nên luôn `NotSubmitted`. Feature `Replaced` đã bị loại khỏi overview như hiện nay.

## 3. Dữ liệu trả về

Thêm một khối `build` vào mỗi phần tử của `FeatureAssignmentOverviewDto`; các trường cũ giữ nguyên, nên frontend hiện tại không bị hỏng.

```json
{
  "featureId": "…",
  "name": "Đặt lịch khám online",
  "status": "Locked",
  "complexity": 3,
  "assignee": { "userId": "…", "displayName": "Dev Test", "branchName": "feature/…", "branchCreated": true },
  "qualifiedMembers": [ … ],
  "build": {
    "state": "Approved",
    "latestSubmissionId": "…",
    "latestSubmittedAt": "2026-11-14T08:00:00Z",
    "failureCode": null,
    "openAlertId": null,
    "lastApproval": {
      "submissionId": "…",
      "approvedAt": "2026-11-14T08:05:00Z",
      "approvedByUserId": "…",
      "approvedByName": "Dev Test",
      "approvedSha": "a1b2c3…"
    }
  }
}
```

- `lastApproval` lấy từ submission `Approved` **gần nhất**, không nhất thiết là submission mới nhất. Ví dụ: feature đã được duyệt lúc 8:05, rồi dev submit thêm lúc 9:00. Khi đó `state = Analyzing`, nhưng frontend vẫn hiện được "đã duyệt lần trước lúc 8:05". `null` nếu chưa từng được duyệt.
- `openAlertId`: id alert đang mở của feature, để frontend mở thẳng alert đó. `null` nếu không có.
- `approvedByName` lấy từ danh sách thành viên workspace **mọi trạng thái** (kể cả đã bị xóa khỏi workspace). Không tìm thấy thì `null`. Entity `BuildSubmission` chỉ có `ApprovedByUserId`, không có navigation sang `User`.
- Không trả `findings`, `suggestions` hay câu trả lời AI. Muốn xem chi tiết thì gọi `GET /build/submissions/{latestSubmissionId}` như hiện nay.

## 4. Truy vấn (tránh N+1)

`GetOverviewAsync` hiện chạy 3 truy vấn. Thêm đúng **3 truy vấn**, không phụ thuộc số feature:

1. **Submission mới nhất của mỗi feature:** repository mới `IBuildSubmissionRepository.GetLatestPerFeatureAsync(workspaceId)`. Dùng `GroupBy(s => s.FeatureId).Select(g => g.OrderByDescending(s => s.CreatedAt).First())`, EF Core 8 dịch được sang `ROW_NUMBER()`. Trả về entity đầy đủ, vì `BuildApprovalPolicy.Evaluate` cần `ExceedsScope` (đọc `FindingsJson`). Số dòng bằng số feature (3 đến 10).
2. **Lần duyệt gần nhất của mỗi feature:** `GetLatestApprovedPerFeatureAsync(workspaceId)`. Cùng cách, lọc `Status == Approved`, **chỉ select** `FeatureId`, `Id`, `ApprovedAt`, `ApprovedByUserId`, `ApprovedSha`, không tải `AiRawResponse`.
3. **Alert:** dùng lại `IDriftAlertRepository.GetByWorkspaceIdAsync(workspaceId, null)`, rồi trong bộ nhớ lấy alert đang mở và alert mới nhất của từng feature (`latestAlertStatus` cho `Evaluate`).

Phải chạy thật truy vấn 1 và 2 trên PostgreSQL (ghi SQL sinh ra bằng log của EF). Nếu Npgsql không dịch được `GroupBy` + `First`, dùng `FromSql` với `SELECT DISTINCT ON (feature_id) … ORDER BY feature_id, created_at DESC`.

**Không sửa** NBA và `VerificationService.InfoAsync` (đang gọi `GetLatestAsync` cho từng feature). Chúng đúng, và MVP chỉ có 3 đến 10 feature; chuyển sang hàm mới là việc riêng, không nằm trong kế hoạch này.

## 5. Các bước và file

1. **Domain:** `ShipMate.Domain/Policies/FeatureBuildStatePolicy.cs`. Nhận submission mới nhất, `hasOpenAlert`, `latestAlertStatus`; trả về `FeatureBuildState` theo bảng ở mục 2. Bên trong gọi `BuildApprovalPolicy.Evaluate`. Enum `FeatureBuildState` đặt trong `ShipMate.Domain/Enums`.
2. **Repository:** thêm 2 hàm ở mục 4 vào `IBuildSubmissionRepository` và `BuildSubmissionRepository`. Thêm record projection `ApprovedSubmissionInfo` cho hàm thứ hai.
3. **DTO:** `FeatureBuildProgressDto` và `FeatureApprovalDto` trong `DTOs/ProductDefinitions/FeatureAssignmentDto.cs` (cùng file với `FeatureAssignmentOverviewDto`); thêm thuộc tính `Build`.
4. **Service:** `FeatureAssignmentService`:
   - Inject thêm `IBuildSubmissionRepository` và `IDriftAlertRepository`.
   - Trong `GetOverviewAsync`: chạy 3 truy vấn, tạo dictionary theo `FeatureId`, và lấy tên người duyệt từ **toàn bộ** thành viên (`GetByWorkspaceIdAsync`, trước khi lọc `Active`). Hiện code chỉ giữ danh sách đã lọc `Active`, nên phải giữ thêm danh sách đầy đủ.
   - Không đổi `ClaimAsync`, `RetryBranchAsync`.
5. **DI:** không cần đăng ký thêm; hai repository đã được đăng ký.
6. **Test:** sửa 2 chỗ gọi constructor: `FeatureAssignmentTests.cs:29` và `Scenarios/LockWorld.cs:77`.
7. **Tài liệu:** thêm mục 2.11 vào `docs/frontend-guide.md` (khối `build`, ý nghĩa từng `state`, "đã duyệt ≠ đã vào `main`", các sự kiện SignalR để tải lại).

Ai bị ảnh hưởng: `FeatureAssignmentService` là code LOCK (Long); phần đọc submission và alert là dữ liệu BUILD (Khoa). Nên báo hai bạn trước khi merge.

## 6. Cập nhật thời gian thực

Không cần sự kiện SignalR mới. Các sự kiện đã gửi tới **cả workspace** đủ để frontend gọi lại `GET /assignments`:

| Sự kiện | Gửi từ | Trạng thái đổi |
|---|---|---|
| `AnalysisCompleted` | `BuildAnalysisService.cs:292` | `Analyzing` sang `ReadyToApprove`, `DriftUnresolved` hoặc `Failed` |
| `DriftAlertTriggered` | `BuildAnalysisService.cs:302` | Sang `DriftUnresolved` |
| `ManagerOverrideExecuted` | `DriftAlertService.cs:110` | `DriftUnresolved` sang `ReadyToApprove` |
| `CodeScopeApproved` | `BuildApprovalService.cs:101` | Sang `Approved` |

**Kẽ hở (đã kiểm tra):** `BuildService` không dùng `IWorkspaceNotifier`, nên lúc dev **submit** (submission mới ở trạng thái `Analyzing`) không có sự kiện nào được gửi. Người khác chỉ thấy `Analyzing` khi tự tải lại, hoặc khi phân tích xong (`AnalysisCompleted`, thường sau 5 đến 15 giây). Có hai cách: thêm sự kiện `SubmissionReceived` trong `BuildService.SubmitAsync` (cần Khoa đồng ý), hoặc chấp nhận độ trễ này.

## 7. Kiểm thử

**Unit, `FeatureBuildStatePolicyTests`** (Domain, không cần mock):
- Không có submission → `NotSubmitted`.
- Mới nhất `Approved` → `Approved`, kể cả khi có alert cũ đã `resolved`.
- Mới nhất `Analyzing` / `Failed` → đúng trạng thái.
- `InScope`, không alert → `ReadyToApprove`.
- `Drift` có alert `unresolved` → `DriftUnresolved`; alert `acknowledged` → vẫn `DriftUnresolved`; alert `overridden` → `ReadyToApprove`.
- `SkippedMaintenance` sau một kết luận vượt scope: alert gần nhất `overridden` → `ReadyToApprove`; ngược lại → `DriftUnresolved`.

**Unit, `FeatureAssignmentTests`** (NSubstitute, như các test đang có):
- Mỗi feature nhận đúng khối `build` của nó; feature không có submission thì `NotSubmitted`, `lastApproval = null`.
- Duyệt lúc 8:05 rồi submit thêm lúc 9:00: `state = Analyzing`, `lastApproval.approvedAt` = 8:05.
- Người duyệt đã bị xóa khỏi workspace thì vẫn có `approvedByName`.
- Repository được gọi đúng **một lần** mỗi loại, dù workspace có nhiều feature (không N+1).

**Chạy thật trên DB dev:**
- Xem SQL của 2 truy vấn mới trong log EF.
- So `state` của từng feature với `GET /next-best-action` và `info.approvedFeatures` của VERIFY: số feature `Approved` phải bằng `approvedFeatures`.
- Dữ liệu: có thể chèn tay submission và alert rồi xóa sau, như ca A ở `04-verify.md` (không tốn lượt AI hay GitHub).

## 8. Cần chốt

| # | Câu hỏi | Đề xuất |
|---|---|---|
| P1 | Đặt khối `build` vào `GET /assignments` hay tạo endpoint riêng `GET /build/progress`? | Vào `/assignments`: frontend đã có màn hình dùng API này, và người nhận nằm sẵn ở đó |
| P2 | Có cần thêm trạng thái "đã vào `main`" không? | Không làm bây giờ. ShipMate không lưu lúc merge PR; muốn có thì phải gọi GitHub hoặc nhận webhook, là việc lớn hơn nhiều |
| P3 | Thêm sự kiện SignalR `SubmissionReceived` lúc submit (mục 6)? | Chưa cần: trạng thái tự cập nhật khi phân tích xong, sau vài giây. Thêm sau nếu frontend thấy cần |
| P4 | Làm trên nhánh nào? | Bạn quyết định (nhánh hiện tại là `feature/verify`) |
