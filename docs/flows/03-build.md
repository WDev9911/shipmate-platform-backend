# Luồng 3 (Build)

Tài liệu này là **kế hoạch chi tiết cài đặt BUILD**, viết trước khi code. Nguyên văn spec BUILD (mục 2.3) nằm ở tài liệu thiết kế của nhóm; ở đây ghi cách code sẽ hiểu và làm, những chỗ spec để ngỏ, và những chỗ phải khớp với VERIFY (2.4, Lâm) và SHIP (2.5, Phúc). Khi từng giai đoạn xong, mục tương ứng được sửa lại thành mô tả phần đã cài đặt, giống `02-lock.md`.

**Mục đích:** code của dev phải qua ShipMate kiểm tra trước rồi mới được vào nhánh chính thức. AI so code với scope đã khóa ở LOCK. Ngoài scope thì cảnh báo và không cho chuyển code sang nhánh feature. Trong scope thì cho chuyển, kèm gợi ý làm code gọn hơn.

**Phân quyền.** Dev submit code, xem kết quả, bấm "Đã xem" và duyệt chuyển nhánh cho feature mình đang giữ. Chỉ Manager được Override cảnh báo drift và chỉnh ngưỡng. Solo dev (người duy nhất trong workspace, kiêm Manager) được Override nhưng bị gắn cờ.

## Mục lục

1. Tiến độ
2. Quyết định đã chốt và đang chờ chốt
3. Hạ tầng GitHub đã chuẩn bị
4. Luồng tổng thể
5. Các lớp rào
6. Mô hình dữ liệu dự kiến
7. Chi tiết từng giai đoạn (GĐ 0 – GĐ 8)
8. Mã lỗi mới
9. Liên quan tới LOCK, VERIFY, SHIP
10. Giới hạn đã biết và cách bù
11. Để sau
12. Việc cần trao đổi với thành viên khác

## 1. Tiến độ (cập nhật 2026-10-22)

| GĐ | Phần | Trạng thái |
|---|---|---|
| 0 | GitHub App, kiểm tra App khi liên kết repo, Bot tạo nhánh và bật Branch Protection (phần LOCK còn nợ) | **Code xong, unit test đạt. Chưa chạy thử trên GitHub thật, chưa chạy migration vào DB dev** |
| 1 | Nhận submit, kiểm tra nhánh và quyền, Governance Suspended | **Code xong, unit test đạt, migration đã chạy vào DB dev.** Chưa thử trên GitHub thật. `SYSTEM_ALERT` đã ghi vào AuditTrail từ GĐ 3 |
| 2 | Phân tích 2 lớp (Rule Engine, AI), chống race | **Code xong, unit test đạt (473/473 toàn bộ), migration đã chạy vào DB dev.** Chưa gọi AI thật, chưa thử trên GitHub thật |
| 3 | Drift alert, Acknowledge, Override, AuditTrail chuỗi hash | **Code xong, unit test đạt (536/536 toàn bộ), migration đã chạy vào DB dev, đã thử tranh chấp ghi chuỗi trên PostgreSQL thật.** Chưa chạy qua HTTP, chưa có alert thật từ AI thật |
| 4 | Duyệt và fast-forward bằng Bot | **Code xong, unit test đạt (569/569 toàn bộ), migration đã chạy vào DB dev.** Chưa gọi GitHub thật, chưa chạy qua HTTP |
| 5 | Check Run (merge gate vào `main`), phát hiện push lén | **Code xong, unit test đạt (598/598 toàn bộ), migration đã chạy vào DB dev.** Chưa thử trên GitHub thật, chưa chạy qua HTTP |
| 6 | Quản lý ngưỡng (khóa kép, Governance Dilution) | **Code xong, unit test đạt (637/637 toàn bộ), migration đã chạy vào DB dev, đã thử tranh chấp đổi ngưỡng trên PostgreSQL thật.** Chưa chạy qua HTTP |
| 7 | NBA Engine | **Code xong, unit test đạt (666/666 toàn bộ), không cần migration.** Chưa chạy qua HTTP, hai truy vấn mới chưa chạy trên PostgreSQL thật |
| 8 | SignalR Hub | **Code xong, unit test đạt (686/686 toàn bộ), không cần migration, đã thử Hub thật bằng máy chủ tối thiểu.** Chưa chạy với app thật, chưa thử với frontend |

### Đã làm (GĐ 0, 2026-10-23, nhánh `feature/build` tạo từ `feature/lock`)

**Hành vi mới**
- Liên kết repo (`POST /workspaces/{id}/github/link`): ngoài việc kiểm tra Manager có quyền với repo, backend hỏi GitHub xem App ShipMate đã cài trên repo chưa. Chưa cài thì trả `GITHUB_APP_NOT_INSTALLED` (409) kèm `details.installUrl`. Cài rồi thì lưu `github_installation_id` và `github_repo_is_organization`.
- Nhận feature (`ClaimAsync`) và thử lại tạo nhánh (`RetryBranchAsync`): Bot (không phải dev) tạo nhánh `feature/{featureId}-{username}` rồi bật Branch Protection ngay. `BranchCreatedAt` chỉ ghi khi cả hai bước xong.
- Repo Organization: luật "chỉ App ShipMate được push, cấm force push, cấm xóa, áp dụng cả cho admin". Repo cá nhân: chỉ cấm force push và xóa; gói không hỗ trợ thì bỏ qua.
- Hủy liên kết repo xóa cả installation.

**Thay đổi cho frontend**
- Lỗi mới `GITHUB_APP_NOT_INSTALLED` khi liên kết repo; dùng `details.installUrl` làm nút "Cài ShipMate App", cài xong bấm liên kết lại.
- `WorkspaceDto` có thêm `gitHubRepoIsOrganization`. `false` thì hiện cảnh báo "repo cá nhân không khóa cứng được".
- Workspace đã liên kết repo từ trước thì cần liên kết lại, nếu không nhận feature chỉ lưu tên nhánh, không tạo nhánh trên GitHub.

**File chính**
| File | Thay đổi |
|---|---|
| `ShipMate.Infrastructure/Services/GitHub/GitHubAppService.cs` (mới) | Ký JWT bằng private key, xin token của Bot (cache gần 1 giờ), tìm installation, tạo nhánh, bật Branch Protection |
| `ShipMate.Application/Interfaces/Services/IGitHubAppService.cs` (mới) | Giao diện của dịch vụ trên |
| `ShipMate.Application/Exceptions/GitHubAppNotInstalledException.cs` (mới) | Mã lỗi `GITHUB_APP_NOT_INSTALLED` |
| `ShipMate.Application/Services/WorkspaceService.cs` | Kiểm tra App khi liên kết, xóa khi hủy liên kết |
| `ShipMate.Application/Services/FeatureAssignmentService.cs` | Bot tạo và khóa nhánh thay cho token của dev |
| `ShipMate.Domain/Entities/Workspace.cs`, `WorkspaceConfiguration.cs`, migration `AddGitHubAppInstallation` | Hai cột mới trên `workspaces` |
| `ShipMate.Application/Interfaces/Services/IGitHubOAuthService.cs` | Bỏ `CreateBranchFromDefaultAsync` (token của dev) vì không còn dùng |
| `ShipMate.API/appsettings.json` | Mục `GitHubApp` (`AppId`, `Slug`, `PrivateKeyPath`), để trống |

**Kiểm thử**
- Unit test: 367/367 đạt. Mới: `GitHubRepoLinkTests` (5 test: cài rồi, repo cá nhân, chưa cài, repo không có quyền, hủy liên kết). Sửa `FeatureAssignmentTests` theo cách tạo nhánh mới, thêm các ca repo cá nhân, repo liên kết trước khi có App, và lỗi bật luật thì thử lại được.
- **Chưa làm:** migration chưa chạy vào DB dev; chưa gọi GitHub thật (token Bot, tìm installation, tạo nhánh, bật luật). `GitHubAppService` chưa có test tự động vì nó gọi trực tiếp GitHub.
- Để thử trên GitHub thật cần: cài App vào `demo-app`, đặt `GitHubApp:AppId` (`5390489`), `GitHubApp:Slug` (`shipmate-bot-dev`), `GitHubApp:PrivateKeyPath` trong `appsettings.Development.json`, chạy migration.

### Đã làm (GĐ 1, 2026-10-23)

**Hành vi mới:** `POST /api/workspaces/{id}/build/submissions` với `{ branchName, commitSha }`. Thành viên active của workspace gọi. Hợp lệ thì trả `202` kèm `{ id, featureId, stagingBranch, headSha, snapshotVersion, status: "Analyzing" }` và lưu một dòng `build_submissions`. Chưa có phân tích: submission đứng ở `Analyzing` cho tới GĐ 2.

Kiểm tra theo thứ tự (dừng ở lỗi đầu tiên):

| # | Tình huống | Mã lỗi (HTTP) |
|---|---|---|
| 1 | Workspace chưa liên kết repo, hoặc liên kết trước khi có App | `WORKSPACE_REPO_NOT_LINKED` (409) |
| 2 | Workspace đang Governance Suspended mà App vẫn chưa được cài lại | `GOVERNANCE_SUSPENDED` (409) |
| 3 | Nhánh `main`, `develop`, `release/*` | `PROTECTED_BRANCH_SUBMIT` (400) |
| 4 | Nhánh `hotfix/*` | `HOTFIX_NOT_SUPPORTED` (400) |
| 5 | Tên nhánh sai mẫu `feature/{guid}-{username}`, hoặc `featureId` không có trong snapshot mới nhất | `INVALID_FEATURE_ID` (400) |
| 6 | Feature `replaced` | `FEATURE_REPLACED` (409) |
| 7 | Feature `backup` | `FEATURE_IS_BACKUP` (409) |
| 8 | Feature `locked`, chưa ai nhận (kể cả assignment đã bị giải phóng) | `FEATURE_NOT_ASSIGNED` (409) |
| 9 | Người khác đang giữ feature | `FEATURE_ASSIGNED_TO_OTHER` (403) |
| 10 | Bạn giữ feature nhưng tên nhánh khác tên nhánh ShipMate đã đăng ký cho bạn | `INVALID_FEATURE_ID` (400) |
| 11 | `commitSha` không phải đầu hiện tại của `staging/{featureId}-{username}` (hoặc nhánh staging chưa có) | `COMMIT_NOT_IN_STAGING` (400) |
| 12 | GitHub không trả lời đúng | `GITHUB_REQUEST_FAILED` (502) |

**Governance Suspended:** khi lấy token Bot mà GitHub báo không còn installation (App bị gỡ), workspace được gắn cờ (`governance_suspended_at`, lý do `APP_UNINSTALLED`) và submit bị từ chối. Mỗi lần submit sau đó, nếu workspace đang bị gắn cờ thì backend kiểm tra lại: App đã cài lại thì tự gỡ cờ và cập nhật installation; chưa thì vẫn `GOVERNANCE_SUSPENDED`. SHIP đọc cột `governance_suspended_at`.

**Quyết định khi cài đặt (khác hoặc chi tiết hơn bản nháp)**
- `commitSha` phải **bằng đầu hiện tại** của nhánh staging, không chỉ "có trên staging". Nếu dev push thêm sau khi CLI báo thì submit đó bị từ chối, CLI báo lại bằng commit mới.
- (Đã làm ở GĐ 3) `SYSTEM_ALERT` được ghi vào AuditTrail ngay lúc gắn cờ. Workspace nào đã bị gắn cờ trước GĐ 3 thì không có bản ghi; cờ trên `workspaces` vẫn là nguồn dữ liệu cho SHIP.
- (Đã làm ở GĐ 2) Trạng thái `Superseded` và hủy phân tích của lần submit trước.
- (Đã làm ở GĐ 2) `GET .../build/submissions/{id}`.
- `build_submissions` mới có các cột GĐ 1 cần; các cột kết quả (điểm, findings, gợi ý...) thêm ở GĐ 2.
- Bộ test: `BuildServiceTests`, `BuildBranchPolicyTests`, `SubmitBuildRequestValidatorTests`; `GitHubAppService` vẫn chưa có test tự động vì gọi thẳng GitHub.

**Thay đổi cho frontend / CLI:** endpoint mới và các mã lỗi ở bảng trên. CLI phải push lên `staging/{featureId}-{username}` trước, rồi gửi đúng SHA vừa push.

**File chính:** `Services/BuildService.cs`, `Policies/BuildBranchPolicy.cs`, `Entities/BuildSubmission.cs`, `Exceptions/BuildExceptions.cs`, `Controllers/BuildController.cs`, `GitHubAppService.GetBranchHeadShaAsync`, migration `AddBuildSubmissions` (bảng `build_submissions` và 2 cột governance trên `workspaces`).

### Đã làm (GĐ 2, 2026-10-23)

**Hành vi mới.** Sau khi submit được nhận, phân tích chạy nền; kết quả đọc bằng `GET /api/workspaces/{id}/build/submissions/{submissionId}` (mọi thành viên active của workspace xem được). Submission đi từ `analyzing` tới một trong:

| Trạng thái | Ý nghĩa |
|---|---|
| `in_scope` | `confidenceScore` ≥ ngưỡng của workspace: code nằm trong scope; có `suggestions` (gợi ý tinh gọn) |
| `drift` | `confidenceScore` < ngưỡng: có `findings` (file và mô tả phần ngoài scope) và `suspectedFeatureIds` |
| `skipped_maintenance` | Rule Engine bỏ qua AI; lặp lại kết quả lần trước, `carriedFromSubmissionId` trỏ về lần đó |
| `superseded` | Có lần submit mới hơn của cùng feature thay thế |
| `failed` | Không ra được kết quả; `failureCode` cho biết lý do |

`failureCode` có thể là: `DIFF_TOO_LARGE`, `AI_RATE_LIMITED`, `AI_UNAVAILABLE`, `AI_OUTPUT_INVALID`, `GITHUB_REQUEST_FAILED`, `GITHUB_APP_UNINSTALLED`, `WORKSPACE_REPO_NOT_LINKED`, `FEATURE_NOT_ACTIVE`, `ANALYSIS_ERROR`.

**Cách hoạt động**
1. **Lấy diff tích lũy:** so nhánh mặc định của repo với commit đã submit (GitHub Compare API, đọc tối đa 3 trang × 100 file/commit). Quá giới hạn của GitHub hoặc tổng patch vượt `Build:MaxDiffCharacters` (mặc định 60.000) thì `DIFF_TOO_LARGE`, **không cắt bớt**.
2. **Lớp 1, Rule Engine** (`MaintenanceChangePolicy`): chỉ xét lần submit này so với lần có kết quả trước đó của feature (cùng version snapshot). Bỏ qua AI khi **mọi** commit mới có message bảo trì (`fix typo`, `refactor`, `bump version`, `format code`) **và** diff mới chỉ là whitespace/comment/format hoặc file `.gitignore`, `README*`, `LICENSE*`, `docs/**`. Có một dòng code thực thi là phải qua AI, message ghi gì cũng vậy. Dòng như `#include`, `*ptr`, `--counter` không bị coi là comment.
3. **Lớp 2, AI** (một lần gọi): nhận feature của dev, các feature còn lại, dependency và diff; trả `confidence_score`, `suspected_feature_ids`, `findings`, `suggestions`. Kết quả được kiểm tra (điểm trong [0,1], id phải là feature khác trong snapshot, không trùng) trước khi lưu.
4. **Ngưỡng:** lấy `DriftConfidenceThreshold` của workspace lúc phân tích và lưu vào `thresholdAtCheck`, nên đổi ngưỡng sau này không đổi kết quả cũ.
5. **Chống race (Session Lock):** submit mới đánh dấu các submission cùng feature còn đang `analyzing` là `superseded` và hủy phân tích đang chạy của feature đó. Kết quả đến muộn của lần cũ bị bỏ, không ghi đè. Các feature khác nhau chạy song song.
6. **Chạy nền:** hàng đợi trong bộ nhớ + `BackgroundService`. Khởi động lại server thì các submission còn `analyzing` được đưa vào hàng đợi lại.

**Quyết định khi cài đặt (khác hoặc chi tiết hơn bản nháp)**
- **Không ghi vào `ai_analysis_runs`.** Bảng đó gắn cứng với `ProductDefinition` của DEFINE. Thông tin truy vết (model, nội dung AI trả về, `failureCode`) nằm ngay trong `build_submissions` (`ai_model`, `ai_raw_response`, `failure_code`), tránh đổi bảng của DEFINE.
- `DIFF_TOO_LARGE` là `failureCode` của submission chứ không phải lỗi HTTP 422, vì phân tích chạy nền sau khi submit đã trả `202`.
- AI so với **snapshot mới nhất lúc phân tích** (không phải lúc submit); nếu feature đã bị loại hoặc thành backup trong lúc chờ thì `FEATURE_NOT_ACTIVE`.
- Không có khác biệt so với `main` thì coi là `in_scope` với điểm 1, không gọi AI.
- Lần có kết quả trước mà thuộc snapshot version cũ, hoặc commit cũ không còn trên GitHub (force push), thì không được dùng để bỏ qua AI.
- Lớp 1 xét gộp mọi commit mới thay vì từng commit, vì chỉ cần một lần gọi Compare; gộp chỉ chặt hơn (có logic ở đâu cũng đẩy sang AI).
- Cửa sổ rất ngắn còn lại: kiểm tra "còn là analyzing" rồi mới lưu không phải thao tác nguyên tử, nên kết quả muộn có thể hiếm khi ghi đè trạng thái `superseded` của chính nó.

**Thay đổi cho frontend / CLI:** endpoint `GET .../build/submissions/{id}`; CLI nên hỏi lại cho tới khi `status` khác `analyzing` (SignalR ở GĐ 8 sẽ thay cho việc hỏi lại). `409` hay `5xx` khi submit không đổi so với GĐ 1.

**Kiểm thử:** 473/473 đạt. Mới: `MaintenanceChangePolicyTests`, `BuildAnalysisServiceTests` (AI giả lập: in scope, drift, ngưỡng, bỏ qua AI, diff quá lớn, lỗi AI, chống race, hủy), `BuildAnalysisQueueTests`, `BuildAnalysisOutputValidatorTests`. **Chưa kiểm chứng:** gọi AI thật (prompt chưa được thử với mô hình thật), Compare API trên GitHub thật, worker nền chạy trong app.

**File chính:** `Services/BuildAnalysisService.cs`, `Services/BuildAnalysisQueue.cs`, `Infrastructure/Services/BuildAnalysisWorker.cs`, `Policies/MaintenanceChangePolicy.cs`, `Ai/Build/*` (prompt, schema, client, validator), `GitHubAppService.CompareAsync`, migration `AddBuildAnalysis`.

### Đã làm (GĐ 3, 2026-10-23)

**Hành vi mới.** Khi phân tích ra `drift`, backend tạo một **cảnh báo** (`drift_alerts`) cùng lúc với kết quả (một lần lưu, nên không có `drift` nào thiếu cảnh báo). Cảnh báo giữ nguyên điểm, ngưỡng, version snapshot và feature nghi ngờ tại lúc tạo.

| Từ | Sang | Ai | API |
|---|---|---|---|
| `unresolved` | `acknowledged` | Dev đang giữ feature | `POST /api/workspaces/{id}/build/alerts/{alertId}/acknowledge` |
| `unresolved`, `acknowledged` | `overridden` | Manager, bắt buộc có `reason` (≤ 1000 ký tự) | `POST .../alerts/{alertId}/override` `{ "reason": "..." }` |
| `unresolved`, `acknowledged` | `resolved` | Tự động | Lần phân tích sau của cùng feature ra `in_scope` |

`GET .../build/alerts?status=unresolved` (bỏ `status` thì lấy tất cả, mới nhất trước) cho mọi thành viên active. Chuyển trạng thái sai (ví dụ override cảnh báo đã `resolved`) trả `INVALID_ALERT_TRANSITION` (409); ack bởi người không giữ feature trả `NOT_FEATURE_HOLDER` (403); override bởi người không phải Manager trả `WORKSPACE_PERMISSION_DENIED` (403).

**AuditTrail (`audit_trail`).** Chuỗi hash theo từng workspace: mỗi bản ghi mang `sequence` (1, 2, 3...), `prev_hash` và `record_hash = SHA-256(prev_hash | sequence | audit_type | actor | created_at | payload)`. Chỉ có thao tác thêm, không có sửa hay xóa trong code. Có hàm kiểm chứng tính lại cả chuỗi và báo `sequence` đầu tiên không khớp (sửa nội dung, sửa giờ, đổi người, xóa giữa chuỗi đều bị phát hiện). Các sự kiện đang được ghi:

| `audit_type` | Khi nào | Ai |
|---|---|---|
| `SNAPSHOT_LOCKED` | Lock lần đầu (`version`, `hash`, số feature) | Manager bấm Lock |
| `SCOPE_CHANGE` | Duyệt Feature Challenge (hành động, `fromVersion`, `toVersion`, hash mới, feature bị bỏ/thay) | Manager duyệt |
| `SCOPE_BYPASS` | Override cảnh báo (alert, feature, submission, lý do, điểm, ngưỡng) | Manager |
| `SOLO_OVERRIDE_LOGGED` | Cùng lúc với `SCOPE_BYPASS` khi workspace chỉ có 1 thành viên active | Manager |
| `SYSTEM_ALERT` | App bị gỡ, workspace bị Governance Suspended (`reason: APP_UNINSTALLED`) | Hệ thống (không có người) |

Mỗi bản ghi nằm **chung một lần lưu** với thay đổi mà nó mô tả (Lock, duyệt challenge, override), nên không thể có thay đổi mà thiếu bản ghi hay ngược lại. Ba loại còn lại của bản nháp (`GOVERNANCE_DILUTION`, `THRESHOLD_CHANGED`, `DIRECT_PUSH_DETECTED`) chưa có vì chưa có luồng nào ghi chúng; `DIRECT_PUSH_DETECTED` đã thêm ở GĐ 5; `GOVERNANCE_DILUTION` và `THRESHOLD_CHANGED` đã thêm ở GĐ 6.

**Quyết định khi cài đặt (khác hoặc chi tiết hơn bản nháp)**
- **Cảnh báo mới thay cảnh báo cũ.** Bản nháp không nói rõ khi feature còn cảnh báo mở mà lại `drift` lần nữa. Làm như sau: cảnh báo cũ chuyển `resolved` với `resolved_by_submission_id` = submission mới (submission đó có trạng thái `drift`, nên phân biệt được "đã sửa xong" với "bị cảnh báo mới thay"), rồi tạo cảnh báo mới. Nhờ vậy mỗi feature chỉ có tối đa một cảnh báo mở và Manager chỉ cần override một lần. **SHIP cần biết điều này** nếu đếm `resolved` như "dev đã sửa".
- `SkippedMaintenance` không tạo và không đóng cảnh báo (đúng bản nháp: "không bao giờ dùng để đóng alert"). `Failed` và `Superseded` cũng không đụng tới cảnh báo.
- `payload` lưu cột `text` chứ không phải `jsonb`: `jsonb` tự sắp xếp lại khóa và khoảng trắng, nên chuỗi đọc ra sẽ khác chuỗi đã băm và mọi lần kiểm chứng đều báo sai.
- Thời gian `created_at` của bản ghi được làm tròn xuống micro giây trước khi băm, vì PostgreSQL chỉ giữ tới micro giây; nếu không, bản đọc lại từ DB sẽ không còn khớp hash.
- `audit_trail` chỉ có khóa ngoại tới `workspaces` (xóa theo), không có khóa ngoại tới `users`: lịch sử không được phụ thuộc vào việc một tài khoản còn tồn tại.
- Tranh chấp ghi chuỗi: chỉ mục unique `(workspace_id, sequence)` chặn hai bản ghi cùng vị trí. Khi lưu thua, `AppDbContext.SaveChangesAsync` đọc lại bản ghi cuối, nối lại các bản ghi đang chờ lên trên nó rồi lưu lại (tối đa 10 lần). Cách này áp dụng cho mọi nơi ghi AuditTrail (LOCK, BUILD) mà không phải sửa từng chỗ.
- **Phát hiện khi thử trên PostgreSQL thật:** nhiều người ghi cùng lúc, mỗi người nhiều bản ghi, còn gây **deadlock** (`40P01`) trên chính chỉ mục đó, không chỉ vi phạm unique. Lần chạy đầu 1/4 người ghi bị lỗi; đã xử lý deadlock như một lần thua tranh chấp (thử lại). Sau khi sửa, 3 lần chạy liên tiếp không còn lỗi.
- Chưa có endpoint gọi hàm kiểm chứng (`IAuditTrail.FindFirstBrokenAsync`): SHIP gọi trực tiếp khi tạo Ship Card. Thêm endpoint khi có nhu cầu.

**Thay đổi cho frontend:** 3 endpoint alert ở trên; `GET .../submissions/{id}` không đổi. Màn hình Build hiện cảnh báo đỏ cho `unresolved`/`acknowledged`, nút Override chỉ cho Manager. Chưa có SignalR nên cần tải lại (GĐ 8).

**Kiểm thử:** 536/536 đạt (thêm 63 so với GĐ 2). Mới: `DriftAlertTests` (mọi chuyển hợp lệ và không hợp lệ), `AuditChainTests` (sửa nội dung/người/giờ, xóa bản ghi, nối lại sau khi thua tranh chấp), `AuditTrailTests`, `DriftAlertServiceTests` (ack đúng/sai người, override đúng/sai quyền, ghi `SCOPE_BYPASS`, ghi chú solo), `OverrideDriftAlertRequestValidatorTests`; thêm vào `BuildAnalysisServiceTests` (tạo, thay thế, đóng cảnh báo), `BuildServiceTests` (`SYSTEM_ALERT`), `LockServiceTests` (`SNAPSHOT_LOCKED`), `FeatureChallengeServiceTests` (`SCOPE_CHANGE`).
**Đã thử trên PostgreSQL thật** (cơ sở dữ liệu tạm `shipmate_audit_race`, đã xóa sau khi chạy, không đụng DB dev): 2, 4, 6 và 8 người ghi cùng lúc vào cùng workspace, tổng 44 bản ghi mỗi lần chạy; chuỗi liền mạch từ 1 tới 44, kiểm chứng không báo lỗi, 3 lần chạy liên tiếp. **Chưa kiểm chứng:** chạy qua HTTP với app thật, cảnh báo sinh ra từ AI thật.

**File chính:** `Domain/Entities/DriftAlert.cs`, `AuditTrailEntry.cs`, `Domain/Policies/AuditChain.cs`, `Domain/Common/AuditTypes.cs`, `Services/DriftAlertService.cs`, `Services/AuditTrail.cs`, `Services/BuildAnalysisService.cs` (`UpdateAlertsAsync`), `Infrastructure/Persistence/AppDbContext.cs` (thử lại khi thua tranh chấp), `Controllers/BuildController.cs`, migration `AddDriftAlertsAndAuditTrail`. Sửa code LOCK: `LockService`, `FeatureChallengeService` (mỗi nơi thêm một lần ghi AuditTrail, đã được duyệt ở quyết định B).

### Đã làm (GĐ 4, 2026-10-23)

**Hành vi mới.** `POST /api/workspaces/{id}/build/submissions/{submissionId}/approve` với body `{}` hoặc `{ "commitSha": "<40 ký tự hex>" }` (không gửi body cũng được). Bot đưa nhánh `feature/{featureId}-{username}` tiến tới commit đã được kiểm tra, rồi xóa nhánh `staging/...` nếu trên đó không còn commit nào mới hơn. Trả về submission với `status: approved`, `approvedSha`, `approvedAt`.

**Ai được duyệt:** chỉ dev đang giữ feature (người được gán, chưa bị thả). Manager không duyệt thay người khác; solo dev thì tự duyệt vì chính là người giữ. Người khác nhận `NOT_FEATURE_HOLDER` (403).

**Điều kiện duyệt** (kiểm tra theo thứ tự này, sai thì không gọi GitHub):

| Tình huống | Kết quả |
|---|---|
| Workspace chưa liên kết repo / đang Governance Suspended | `WORKSPACE_REPO_NOT_LINKED` / `GOVERNANCE_SUSPENDED` |
| Submission không thuộc workspace | `NOT_FOUND` |
| Feature đã bị loại hoặc thành backup | `FEATURE_REPLACED` / `FEATURE_IS_BACKUP` |
| Có submission mới hơn của feature, hoặc submission này đã `superseded` | `SUBMISSION_SUPERSEDED` |
| Đang `analyzing`, `failed`, hoặc đã `approved` rồi | `SUBMISSION_NOT_READY` |
| Feature còn cảnh báo `unresolved` hoặc `acknowledged` ("Đã xem" không mở khóa) | `DRIFT_UNRESOLVED` |
| Điểm thấp hơn ngưỡng mà cảnh báo mới nhất của feature chưa `overridden` | `DRIFT_UNRESOLVED` |
| `commitSha` khác đầu của submission mà không nằm trong các commit nhánh `feature/...` chưa có | `COMMIT_NOT_IN_SUBMISSION` (400) |
| GitHub từ chối vì lệch lịch sử (không fast-forward được) hoặc không còn commit | `FAST_FORWARD_FAILED` (409): rebase rồi submit lại |

**Cách làm.** `PATCH /repos/{o}/{r}/git/refs/heads/feature/...` với `{ sha, force: false }` bằng token Bot (nhánh đã khóa "chỉ Bot ghi" từ GĐ 0). Thành công thì lưu `approved_sha`/`approved_by_user_id`/`approved_at` trên submission và `last_approved_sha` trên `feature_assignments` (GĐ 5 dùng để phát hiện push lén). Việc dọn nhánh staging làm **sau** khi đã lưu và lỗi ở bước này bị bỏ qua: nhánh staging còn lại không gây hại, còn hoàn tác một lần duyệt đã xong thì có hại.

**Quyết định khi cài đặt (khác hoặc chi tiết hơn bản nháp)**
- **Chọn lọc commit** so với đầu hiện tại của nhánh `feature/...` (Compare API) chứ không so với `last_approved_sha` như bản nháp. Hai giá trị bằng nhau khi chỉ Bot ghi; khi repo cá nhân bị push lén thì đầu thật mới đúng. Lệch lịch sử thì GitHub từ chối ở bước fast-forward.
- Chọn commit sớm hơn đầu của submission thì **không xóa staging** (trên đó còn commit chưa duyệt). Phần diff của commit sớm hơn đã được AI xem như một phần của diff tích lũy đến đầu submission, chứ không được phân tích riêng.
- Submission `skipped_maintenance` được duyệt theo kết quả mà nó lặp lại: mang kết quả trong scope thì duyệt được; mang kết quả drift thì cần cảnh báo drift gần nhất đã `overridden`.
- Không yêu cầu submission phải phân tích đúng version snapshot mới nhất. Đổi scope bằng Feature Challenge chỉ thêm/bỏ/thay feature, còn feature của dev vẫn kiểm tra là còn `locked`.
- Ghi vào `build_submissions.status` thêm `approved`. Rule Engine vẫn lấy submission đã duyệt làm mốc so sánh cho lần sau (kết quả cũ còn nguyên điểm và gợi ý).
- Lần duyệt không ghi AuditTrail (bản nháp không liệt kê loại này); sự kiện `CodeScopeApproved` của spec sẽ đi qua SignalR ở GĐ 8. (Đã làm ở GĐ 5) Check Run ✅ lên commit đã duyệt.
- Bot bị gỡ giữa chừng thì `approve` trả `GITHUB_APP_UNINSTALLED` (409); cờ Governance Suspended được bật ở lần submit kế tiếp, không bật ở đây.
- Nếu GitHub đã fast-forward mà lưu DB lỗi, gọi lại `approve` là chạy lại được (đẩy nhánh tới cùng commit là thao tác lặp lại an toàn).

**Thay đổi cho frontend / CLI:** nút "Duyệt" chỉ bật khi submission `in_scope` (hoặc `drift` đã được Manager override) và là submission mới nhất; hiển thị lỗi `FAST_FORWARD_FAILED` kèm hướng dẫn rebase. `GET .../submissions/{id}` có thêm `approvedSha`, `approvedAt`.

**Kiểm thử:** 569/569 đạt (thêm 33). Mới: `BuildApprovalServiceTests` (duyệt thành công, xóa/giữ staging, lỗi dọn staging, kết quả drift có/không override, cảnh báo mở, ack không mở khóa, submission cũ/đang chạy/thất bại/đã duyệt, người không giữ feature, feature bị loại, mất App, chọn commit sớm hơn và commit ngoài khoảng, GitHub từ chối), `ApproveBuildRequestValidatorTests`. **Chưa kiểm chứng:** gọi GitHub thật (PATCH ref, DELETE ref, Compare lấy danh sách commit), gọi qua HTTP với body rỗng.

**File chính:** `Services/BuildApprovalService.cs`, `GitHubAppService.FastForwardBranchAsync` / `DeleteBranchAsync`, `BuildController.Approve`, `DTOs/Build/BuildSubmissionDto.From`, migration `AddBuildApproval`.

### Đã làm (GĐ 5, 2026-10-23)

**Hành vi mới**

1. **Merge gate khi liên kết repo.** `POST /workspaces/{id}/github/link` giờ còn bật luật cho nhánh mặc định: muốn vào nhánh đó phải có check `ShipMate Scope Check` do **chính App này** tạo. Kết quả trả về trong `WorkspaceDto.gitHubMergeGateEnabled` (`false` thì frontend hiện cảnh báo "chưa khóa được merge vào nhánh chính").
   - Nhánh chưa có luật nào: tạo luật mới (bắt buộc check, **áp dụng cả cho admin**, không đòi review, không giới hạn người push khác).
   - Nhánh đã có luật và đã đòi status check: **thêm** check của ShipMate vào danh sách, giữ nguyên các check khác và mọi luật còn lại; đã có sẵn thì không làm gì.
   - Nhánh đã có luật nhưng **chưa đòi status check nào**: không đụng tới, `gitHubMergeGateEnabled = false`. Thêm check vào trường hợp này nghĩa là viết lại toàn bộ luật (review, giới hạn push...) mà chưa thử trên GitHub thật, nên chưa làm.
   - Repo cá nhân không đặt được luật (GitHub trả 403/404) thì `false`, vẫn liên kết bình thường. Repo Organization mà GitHub từ chối thì `GITHUB_REQUEST_FAILED` (502) và **không lưu liên kết**.
   - Hủy liên kết xóa cờ merge gate và mốc kiểm tra `main`. Luật đã đặt trên GitHub không bị gỡ.
2. **Check Run khi duyệt.** `approve` (GĐ 4) tạo Check Run `ShipMate Scope Check` thành công trên commit được duyệt (kèm tên feature, version scope, điểm và ngưỡng; ghi rõ nếu là drift được Manager override) **trước** khi di chuyển nhánh. Nhờ vậy nếu GitHub từ chối tạo check thì chưa có gì thay đổi và duyệt lại được. Kết quả: PR `feature/...` → `main` có ✅ nên merge được; PR khác không có ✅ nên bị khóa.
3. **Phát hiện push lén** chạy ở mỗi lần submit hợp lệ (sau khi đã qua mọi kiểm tra của GĐ 1, trước khi ghi submission), ghi `DIRECT_PUSH_DETECTED` vào AuditTrail, lưu chung một lần với submission:
   - **Nhánh `feature/...` của dev:** đầu nhánh khác `last_approved_sha` thì ghi (`headSha`, `expectedSha`). Chưa duyệt lần nào thì so với `main`: có commit nằm ngoài lịch sử `main` thì ghi. Đầu nhánh đã báo được nhớ (`feature_assignments.flagged_head_sha`) nên không báo lại mỗi lần submit; push thêm thì báo tiếp.
   - **Nhánh mặc định:** mỗi commit mới từ `workspaces.last_audited_main_sha` phải thuộc một PR đã merge **và** commit đầu của PR đó có check ✅ của ShipMate. Không có PR thì `reason: NO_PULL_REQUEST`, có PR nhưng thiếu check thì `NO_SCOPE_CHECK`. Lịch sử `main` bị viết lại (commit mốc không còn) thì `HISTORY_REWRITTEN`. Kiểm tra theo PR nên merge kiểu squash vẫn đúng.
   - Lần kiểm tra đầu tiên của một workspace chỉ **đặt mốc** ở đầu `main` hiện tại, vì phần lịch sử trước ShipMate không thể xét.
   - Lỗi từ GitHub không làm hỏng submit: phát hiện kết thúc êm, các bản ghi dự kiến ghi chỉ được ghi khi đã xem hết các commit, nên lần sau chạy lại không bị báo trùng.

**Quyết định khi cài đặt (khác hoặc chi tiết hơn bản nháp), cần bạn xem**
- **Luật mới trên nhánh chính áp dụng cả cho admin** (`enforce_admins = true`). Hệ quả: kể cả chủ Organization cũng không push thẳng vào `main` được và phải qua PR có ✅. Bản nháp chỉ nói "bắt buộc check", không nói admin. Chọn bật vì không bật thì người tạo Organization (thường là Manager) lách được ngay. Muốn nới thì đổi một dòng trong `GitHubAppService.EnsureMergeGateAsync`.
- Workspace **đã liên kết repo trước GĐ 5 chưa có merge gate**; phải hủy liên kết rồi liên kết lại (chưa có endpoint "bật lại merge gate", thêm khi cần).
- Nhánh chưa duyệt lần nào: bản nháp không nói cách xét. Dùng "có commit ngoài lịch sử `main`" thay vì bỏ qua, vì đây chính là lúc repo cá nhân dễ bị push lén nhất.
- Quét `main` đọc tối đa 300 commit mỗi lần (giới hạn Compare API đã dùng ở GĐ 2); nhiều hơn thì lần submit sau đọc tiếp từ commit cuối đã xét.
- Check ❌ cho submission drift không được tạo (bản nháp chỉ nói tạo ✅ khi duyệt); drift chưa override thì không duyệt được nên không có ✅, PR tự bị khóa.
- Quyền App cần: Checks (ghi) để tạo Check Run; đọc PR của commit nằm trong quyền Contents. Cả hai đã có (Checks RW, Contents RW).

**Thay đổi cho frontend:** `WorkspaceDto.gitHubMergeGateEnabled`; cảnh báo khi `false` hoặc `gitHubRepoIsOrganization = false`. Không có endpoint mới.

**Kiểm thử:** 598/598 đạt (thêm 29 so với GĐ 4). Mới: `DirectPushDetectorTests` (nhánh feature khớp/lệch/báo một lần/chưa duyệt lần nào/bị xóa; `main`: lần đầu đặt mốc, commit qua PR có/không check, không có PR, nhiều commit một PR chỉ hỏi check một lần, lịch sử bị viết lại, quá giới hạn, GitHub lỗi giữa chừng, chuỗi hash vẫn liền); thêm vào `BuildApprovalServiceTests` (Check Run đúng commit, drift override, lỗi tạo check thì chưa đổi gì, từ chối thì không tạo check), `GitHubRepoLinkTests` (lưu cờ merge gate, GitHub từ chối thì không liên kết, hủy liên kết), `BuildServiceTests` (detector chạy khi submit hợp lệ, không chạy khi bị từ chối). **Chưa kiểm chứng trên GitHub thật:** đặt/mở rộng luật nhánh chính (đặc biệt `app_id` trong `checks` và PATCH `required_status_checks`), tạo Check Run, `commits/{sha}/pulls`, `check-runs?app_id=`. Các mục trong "Test" của GĐ 5 (PR từ nhánh lạ bị khóa, PR từ `feature/...` merge được, push thẳng vào `main` repo cá nhân có bản ghi) vẫn cần chạy thật khi bạn cho phép.

**File chính:** `Services/DirectPushDetector.cs`, `Services/BuildApprovalService.cs` (Check Run), `Services/WorkspaceService.cs` (merge gate), `GitHubAppService.EnsureMergeGateAsync` / `CreateScopeCheckRunAsync` / `HasPassingScopeCheckAsync` / `GetMergedPullRequestsOfCommitAsync`, migration `AddMergeGateAndPushDetection` (`workspaces.github_merge_gate_enabled`, `workspaces.last_audited_main_sha`, `feature_assignments.flagged_head_sha`).

### Đã làm (GĐ 6, 2026-10-23)

**Hành vi mới.** `PUT /api/workspaces/{id}/drift-threshold` với `{ "value": 0.8 }`, **chỉ Manager**. Trả về `{ value, changesUsed, maxChanges, nextChangeAllowedAt }` (`nextChangeAllowedAt` là `null` khi đã hết lượt). Ngưỡng mặc định vẫn là 0.75 (cột `drift_confidence_threshold` có từ trước).

| Luật | Mã lỗi |
|---|---|
| Giá trị phải trong [0.70, 0.85] (sàn cứng hệ thống 0.65 nằm dưới khoảng này) | `THRESHOLD_OUT_OF_RANGE` (400, `details.min/max`) |
| Đặt đúng giá trị đang có | `THRESHOLD_UNCHANGED` (400) |
| Đã đủ 3 lần đổi trong cả dự án | `THRESHOLD_CHANGE_LIMIT_REACHED` (409) |
| Lần đổi trước chưa quá 7 ngày | `THRESHOLD_CHANGE_COOLDOWN` (409, `details.nextChangeAllowedAt`) |
| Hai lần đổi chạy đồng thời, một bên thua | `THRESHOLD_CHANGE_CONFLICT` (409): tải lại giá trị mới rồi thử lại |
| Giá trị có quá 2 chữ số thập phân | `VALIDATION_FAILED` (400) |
| Người gọi không phải Manager | `WORKSPACE_PERMISSION_DENIED` (403) |

Thứ tự kiểm tra: ngoài khoảng, giống giá trị cũ, hết 3 lượt, chưa đủ 7 ngày. Lần đổi đầu tiên được làm ngay.

**Ghi vết (AuditTrail, chung một lần lưu với việc đổi giá trị):**
- Mỗi lần đổi: `THRESHOLD_CHANGED` (`from`, `to`, `changeNumber`, người đổi).
- Nếu **hạ** ngưỡng: ghi thêm `GOVERNANCE_DILUTION` (`from`, `to`) để SHIP trừ điểm. Nâng ngưỡng không ghi.
- Số lần đổi và thời điểm lần đổi gần nhất **đếm từ các bản ghi `THRESHOLD_CHANGED`** của workspace, không tin dữ liệu từ client và không reset theo snapshot version, nên mở Feature Challenge để lấy thêm lượt không có tác dụng. Hủy rồi liên kết lại repo cũng không đặt lại số lần.
- Cảnh báo drift cũ giữ `threshold_at_check` lúc tạo, không bị tính lại.

**Quyết định (bạn đã chọn):** **mọi lần đổi đều tính lượt, kể cả nâng ngưỡng** (cách A). Spec dùng chữ "hiệu chỉnh" cho giới hạn lượt nhưng chỉ nhắc `GOVERNANCE_DILUTION` cho lần hạ, nên đây là chỗ spec chưa quy định rõ; nếu thầy hỏi thì trả lời theo cách A và có thể hỏi lại thầy Vũ.

**Quyết định khi cài đặt (chi tiết hơn bản nháp)**
- **Chống đổi đồng thời bằng khóa lạc quan:** `drift_confidence_threshold` là concurrency token, nên một lần lưu chỉ thành công nếu giá trị vẫn là giá trị mà lần đổi đó dựa vào. Chỉ đếm từ AuditTrail thì hai Manager (hoặc hai tab) cùng bấm sẽ cùng thấy "chưa đổi lần nào" và cùng thắng, phá luật 7 ngày; xem mục kiểm thử.
- Sàn 0.65, khoảng 0.70 đến 0.85, 3 lần và 7 ngày là hằng số trong `DriftThresholdPolicy`. Spec giao việc giữ sàn cho Admin, nhưng module cấu hình của Admin chưa có.
- Đặt đúng giá trị đang có thì bị từ chối (không tính là thay đổi). Không có trong bản nháp.
- Mã `THRESHOLD_UNCHANGED` và `THRESHOLD_CHANGE_CONFLICT` là mã **thêm mới**, không có trong bảng mã lỗi bản nháp.
- Một cập nhật khác lên cùng workspace (ví dụ gỡ cờ Governance Suspended) mà trùng thời điểm với một lần đổi ngưỡng cũng sẽ bị coi là thua tranh chấp. Rất hiếm.

**Endpoint xem lịch sử (thêm 2026-10-29):** `GET /api/workspaces/{id}/drift-threshold`, mọi thành viên active gọi được (người ngoài nhận `NOT_FOUND`). Trả `{ value, changesUsed, maxChanges, nextChangeAllowedAt, history: [{ changeNumber, from, to, changedByUserId, changedAt }] }`, `history` cũ nhất trước. `nextChangeAllowedAt` là `null` khi hết lượt; chưa đổi lần nào hoặc đã qua 7 ngày thì bằng thời điểm gọi (đổi được ngay). Đọc từ các bản ghi `THRESHOLD_CHANGED` trong AuditTrail, không cần migration. Chỉ trả `changedByUserId`, không kèm tên: frontend lấy tên từ danh sách thành viên.

**Thay đổi cho frontend:** màn hình chỉnh ngưỡng chỉ cho Manager; hiển thị `changesUsed/maxChanges`, `nextChangeAllowedAt` và lịch sử từ `GET .../drift-threshold`; xử lý các mã lỗi ở bảng trên.

**Kiểm thử:** 637/637 đạt (thêm 39 so với GĐ 5). Mới: `DriftThresholdPolicyTests` (khoảng, biên 7 ngày, tính từ lần gần nhất, nâng và hạ dùng chung lượt, đủ 3 lần), `DriftThresholdServiceTests` (đổi thành công, hạ ghi thêm Dilution, nâng không ghi, chỉ đếm `THRESHOLD_CHANGED`, lần thứ 3 hết lượt, từ chối thì không đổi gì và không ghi vết, không phải Manager), `ChangeDriftThresholdRequestValidatorTests`.
**Đã thử trên PostgreSQL thật** (DB tạm `shipmate_threshold_race`, đã xóa sau khi chạy, không đụng DB dev): hai Manager đổi ngưỡng cùng lúc (0.80 và 0.70), cả hai đã đọc giá trị cũ trước khi lưu. **Lần chạy đầu cả hai cùng thành công**, vì chưa bật khóa lạc quan: lỗi thật mà unit test không thấy. Sau khi bật, 3 lần chạy liên tiếp: một bên thành công, bên kia nhận `THRESHOLD_CHANGE_CONFLICT`, DB chỉ có đúng 1 bản ghi `THRESHOLD_CHANGED`, chuỗi hash liền. Đổi lần hai ngay sau đó bị `THRESHOLD_CHANGE_COOLDOWN`. **Chưa kiểm chứng:** gọi qua HTTP với app thật.

**File chính:** `Domain/Policies/DriftThresholdPolicy.cs`, `Services/DriftThresholdService.cs`, `Controllers/DriftThresholdController.cs`, `Exceptions/DriftThresholdExceptions.cs`, `AppDbContext.SaveChangesAsync` (đổi lỗi tranh chấp thành `THRESHOLD_CHANGE_CONFLICT`), `WorkspaceConfiguration` (concurrency token), `IAuditTrailRepository.GetByTypeAsync`, migration `DriftThresholdConcurrencyToken` (migration rỗng, chỉ cập nhật mô hình).

### Đã làm (GĐ 7, 2026-10-23)

**Hành vi mới.** `GET /api/workspaces/{id}/next-best-action`, mọi thành viên active gọi, kết quả nói về **người đang gọi**. Dùng luật, **không gọi AI**, không ghi gì vào DB. Luôn trả đúng một hành động:

```json
{ "action": "FixDrift", "featureId": "...", "featureName": "Booking", "alertId": "...",
  "submissionId": null, "blockedReason": null, "blockedByFeatureId": null }
```

| Thứ tự | `action` | Khi nào | Trường đi kèm |
|---|---|---|---|
| 1 | `FixDrift` | Feature của bạn đang có cảnh báo `unresolved` hoặc `acknowledged` | `featureId`, `featureName`, `alertId` |
| 2 | `ApproveSubmission` | Submission mới nhất của feature bạn đã qua kiểm tra, chờ bạn duyệt | `featureId`, `featureName`, `submissionId` |
| 3 | `WorkOnFeature` | Feature của bạn chưa có submission, hoặc submit gần nhất `failed`/`analyzing`, và không bị chặn | `featureId`, `featureName` |
| 4 | `ClaimFeature` | Bạn không còn việc: có feature `locked` còn trống, vừa skill, không bị chặn | `featureId`, `featureName` |
| 5 | `Blocked` | Việc còn lại của bạn đều bị feature tiền đề chặn | thêm `blockedReason`, `blockedByFeatureId` |
| 6 | `NoAction` | Chưa Lock lần nào, hoặc không còn gì để gợi ý | không |

`blockedReason` (giá trị theo spec): `dependency_pending_assignment` (tiền đề đang `backup`, chưa ai nhận) hoặc `dependency_drift_unresolved` (tiền đề có cảnh báo `unresolved`/`acknowledged`). Cả hai cùng đúng thì báo `backup` trước. `acknowledged` vẫn chặn, đúng spec.

**Cách chọn trong từng bước**
- Có nhiều feature cùng thỏa thì chọn theo tên; riêng `ClaimFeature` chọn feature **nhiều feature khác phụ thuộc nhất** (nhận nó mở được nhiều việc nhất), hòa thì theo tên.
- Feature đã bị thả (`released`) hoặc đã `replaced` không tính là của bạn; feature bị thả thì ai cũng nhận lại được.
- Code đã `approved` được coi là xong cho tới khi bạn submit thêm; chỉ `Approved` mới được coi là xong, các trạng thái khác của submission mới nhất vẫn tính là còn việc.
- "Đủ điều kiện duyệt" dùng **chung một luật** với `approve` (`BuildApprovalPolicy`): mới nhất của feature, trong scope hoặc drift đã được Manager override, không còn cảnh báo mở. NBA gợi ý duyệt thì `approve` chắc chắn qua phần kiểm tra này. Trước đó luật nằm riêng trong `BuildApprovalService`; đã tách ra và 637 test cũ vẫn đạt.

**Quyết định khi cài đặt (khác hoặc chi tiết hơn bản nháp)**
- Thêm bước **`Blocked`** và **`NoAction`** làm kết quả cuối vì bản nháp yêu cầu "đúng một hành động, kèm `blockedReason` khi bị chặn" nhưng không nói khi *mọi* việc đều bị chặn thì trả gì.
- Bị chặn mà còn feature trống để nhận thì gợi ý **nhận feature** trước, vì chờ không làm tiến độ nhích lên (bản nháp xếp "nhận feature" ở bước 4, trước cả khi báo bị chặn). Thứ tự này do mình giữ theo bản nháp.
- Feature nhận được phải **không bị chặn**; nhận một feature mà tiền đề của nó đang kẹt thì chỉ chuyển sang kẹt.
- Chỉ xét `skillLevel` của bạn (tự khai ở LOCK) so với độ phức tạp; không kiểm tra thêm `github_username` hay repo, những thứ `claim` sẽ báo lỗi riêng.
- NBA chưa gồm `verify_checklist_incomplete`: VERIFY chưa có bảng checklist. Khi có, nối vào bước "mọi feature của version đã duyệt mà checklist chưa xong" (xem mục 12).
- Thêm hai hàm đọc cho repo cảnh báo: `GetOpenByWorkspaceAsync` (NBA dùng) và `HasOpenDriftAsync(workspaceId, snapshotVersion)` (cho VERIFY, đã nêu trong bản nháp; NBA không dùng).

**Thay đổi cho frontend:** trang chủ dev gọi endpoint này và vẽ một thẻ theo `action`; bấm vào thẻ dẫn tới màn hình tương ứng (alert, submission, feature). Không có SignalR nên cần tải lại; khi xong GĐ 8 sẽ có event báo "hết bị chặn".

**Kiểm thử:** 666/666 đạt (thêm 29 so với GĐ 6). Mới: `NextBestActionServiceTests` (chưa Lock, drift của mình và của người khác, duyệt được/không duyệt được, ưu tiên duyệt trước làm việc khác, các trạng thái submit, feature bị thả/bị loại, bị chặn theo `backup` và theo drift, `acknowledged` vẫn chặn, hai lý do cùng đúng, feature bị chặn không cản việc khác, nhận feature theo skill và theo độ phụ thuộc). **Chưa kiểm chứng:** hai truy vấn mới (`GetOpenByWorkspaceAsync`, `HasOpenDriftAsync`) chưa chạy trên PostgreSQL thật, endpoint chưa gọi qua HTTP.

**File chính:** `Services/NextBestActionService.cs`, `Domain/Policies/BuildApprovalPolicy.cs`, `Controllers/NextBestActionController.cs`, `DTOs/Build/NextBestActionDto.cs`. Không có migration.

### Đã làm (GĐ 8, 2026-10-23)

**Cách kết nối (cho frontend và CLI).** Hub ở `/hubs/workspace`, dùng thư viện SignalR client (SignalR chạy trên WebSocket và tự lùi sang cách khác nếu WebSocket bị chặn). Phải có JWT; trình duyệt không gắn được header vào WebSocket nên token đi qua `?access_token=<jwt>` (client SignalR tự làm khi khai `accessTokenFactory`). Server chỉ đọc token từ query ở đường dẫn `/hubs/workspace`.

1. Kết nối xong, connection tự vào nhóm riêng của người dùng (`user:{userId}`) để nhận tin gửi riêng.
2. Gọi `JoinWorkspace(workspaceId)` để nghe tin của một workspace. Chỉ thành viên **active** vào được, người khác nhận `WORKSPACE_NOT_FOUND` (giống như workspace không tồn tại). `LeaveWorkspace(workspaceId)` để rời.

**Các sự kiện** (payload là JSON, khóa viết thường đầu):

| Sự kiện | Khi nào | Gửi tới | Nội dung chính |
|---|---|---|---|
| `AnalysisCompleted` (thêm) | Phân tích có kết quả (kể cả `Failed`, `SkippedMaintenance`); không gửi nếu kết quả bị bỏ vì có submit mới hơn | cả workspace | `submissionId`, `featureId`, `status`, `failureCode` |
| `DriftAlertTriggered` | Phân tích ra drift, cảnh báo mới được tạo | cả workspace | `alertId`, `featureId`, `submissionId`, `confidenceScore`, `thresholdAtCheck`, `suspectedFeatureIds` |
| `OptimizationSuggested` | Trong scope và có gợi ý tinh gọn | cả workspace | `submissionId`, `featureId`, `suggestionCount` |
| `DriftAlertAcknowledged` | Dev bấm "Đã xem" | cả workspace | `alertId`, `featureId`, `acknowledgedBy` |
| `ManagerOverrideExecuted` | Manager override | cả workspace | `alertId`, `featureId`, `overriddenBy`, `reason`, `solo` |
| `CodeScopeApproved` | Dev duyệt, Bot đã fast-forward | cả workspace | `submissionId`, `featureId`, `featureName`, `approvedSha`, `approvedBy` |
| `BranchPushSuccess` | Cùng lúc với `CodeScopeApproved` | cả workspace | `featureId`, `branch`, `sha` |
| `BackupFeatureClaimed` (thêm) | Một feature `backup` được nhận | **riêng** dev đang giữ feature phụ thuộc vào nó (trừ người vừa nhận) | `workspaceId`, `featureId`, `featureName`, `claimedBy` |
| `FeatureRemoved` (thêm, nợ LOCK) | Feature bị bỏ khi duyệt Remove & Replace | **riêng** dev giữ feature bị bỏ và mọi feature phụ thuộc vào nó | `workspaceId`, `featureId`, `featureName`, `dependentFeatureIds` |
| `BackupFeatureMatched` (thêm, nợ LOCK) | Dev khai hoặc sửa skill, hoặc nhận lời mời vào workspace, và có feature `backup` vừa với skill | **riêng** người đó | `workspaceId`, `features[{featureId, featureName}]` |

Tin chỉ được gửi **sau khi** dữ liệu đã lưu, nên khi client nhận thì đọc lại qua API sẽ thấy trạng thái mới. Gửi tin lỗi thì chỉ ghi cảnh báo vào log, **không làm hỏng** việc đã làm (duyệt, override...). Client lỡ tin vẫn đọc được trạng thái qua API; vì vậy nên coi SignalR là "báo để tải lại", không phải nguồn dữ liệu duy nhất.

Gộp xong hai thông báo LOCK còn nợ (`FeatureRemoved`, `BackupFeatureMatched`); `02-lock.md` đã cập nhật.

**Quyết định khi cài đặt (khác hoặc chi tiết hơn bản nháp)**
- **Tên và dạng tin do mình đặt cho 3 sự kiện LOCK/BUILD mới** (`AnalysisCompleted`, `BackupFeatureClaimed` có trong bản nháp; `FeatureRemoved`, `BackupFeatureMatched` thì bản nháp chỉ nói "gộp 2 thông báo LOCK" mà chưa đặt tên). Frontend cần thống nhất với tên này.
- **Tin theo `suspected_feature_ids`:** spec muốn báo cho thành viên liên quan tới các feature bị nghi. Làm là gửi cho cả workspace kèm `suspectedFeatureIds`, để frontend tự lọc. Chưa gửi riêng cho người giữ từng feature đó.
- `BranchPushSuccess` **chưa kèm danh sách commit** như spec ("kèm danh sách commit"); cần thêm một lần gọi GitHub (Compare) lúc duyệt. Mới có `sha` đã được đưa vào nhánh.
- Nhắc "backup vừa skill" chỉ khi **chính người đó** khai hoặc sửa skill hoặc vào workspace. Khi Manager thêm feature `backup` mới qua Feature Challenge thì chưa báo cho các dev vừa skill (bản nháp LOCK cũng chỉ nêu hai trường hợp đầu).
- Người bị loại khỏi workspace vẫn nghe tin của workspace đó **đến khi connection hiện tại ngắt** (nhóm không bị gỡ lúc loại). Kết nối lại thì không vào được nữa vì `JoinWorkspace` kiểm tra lại. Chưa làm gỡ ngay.
- Token trong URL (`access_token`) có thể xuất hiện trong log của máy chủ web hoặc proxy. Là cách chuẩn của SignalR cho WebSocket; token chỉ sống ngắn (JWT access token).
- Giả định một instance server. Chạy nhiều instance thì tin chỉ tới client đang nối vào đúng instance đó (muốn mở rộng cần Redis backplane, chưa làm).

**Thay đổi cho frontend / CLI:** nối vào hub, `JoinWorkspace`, lắng nghe các sự kiện ở bảng trên. CLI dùng `AnalysisCompleted` thay cho việc hỏi lại `GET .../submissions/{id}`; trang chủ dùng các sự kiện để gọi lại `GET .../next-best-action`.

**Kiểm thử:** 686/686 đạt (thêm 20 so với GĐ 7): mỗi nơi phát tin có test (có phát đúng sự kiện, đúng người nhận, không phát khi việc bị từ chối hoặc kết quả bị bỏ), gồm cả `FeatureRemoved` đúng người giữ feature bị bỏ và feature phụ thuộc, `BackupFeatureClaimed` không gửi cho chính người nhận, `BackupFeatureMatched` theo skill.
**Đã thử Hub thật** (máy chủ Kestrel tối thiểu trong thư mục tạm, dùng đúng lớp `WorkspaceHub` và `SignalRWorkspaceNotifier`, cùng đoạn cấu hình JWT-qua-query, client SignalR thật): không token thì bị từ chối; token trong query thì vào được; người không phải thành viên `JoinWorkspace` bị từ chối; thành viên vào nhóm thì nhận tin của workspace còn người ngoài nhóm thì không; tin gửi riêng tới đúng người dù họ chưa vào workspace nào; sau `LeaveWorkspace` không nhận nữa. **Chưa kiểm chứng:** chạy với app thật (đoạn cấu hình trong `Program.cs` được copy sang máy chủ thử chứ chưa chạy chính nó), frontend thật.

**File chính:** `ShipMate.API/Hubs/WorkspaceHub.cs`, `Hubs/SignalRWorkspaceNotifier.cs`, `Program.cs` (SignalR, đọc token từ query, `MapHub`), `Application/Interfaces/Services/IWorkspaceNotifier.cs` (giao diện và tên sự kiện); các điểm phát tin: `BuildAnalysisService`, `DriftAlertService`, `BuildApprovalService`, `FeatureChallengeService`, `FeatureAssignmentService`, `WorkspaceService`. Không có migration.

## 2. Quyết định

### Đã chốt

| # | Quyết định | Lý do |
|---|---|---|
| Q1 | **Mức 3 (khóa cứng)**: GitHub App, Branch Protection "chỉ Bot ghi", Check Run khóa merge | Spec yêu cầu "chỉ ShipMate Bot được ghi" và Check Run. Chỉ GitHub App mới tạo được Check Run, và Bot phải có danh tính riêng thì GitHub mới phân biệt được với dev |
| Q3 | **Liên kết repo giữ luồng cũ, thêm kiểm tra App** (cách B) | Ít thay đổi nhất; frontend chỉ cần hiển thị thêm mã lỗi `GITHUB_APP_NOT_INSTALLED` kèm link cài App |

### Đã chốt ngày 2026-10-23 (theo đề xuất)

| # | Câu hỏi | Quyết định |
|---|---|---|
| 2a | Làm phần LOCK còn nợ: Bot tạo nhánh `feature/...` và bật Branch Protection ngay khi dev nhận feature | **Làm.** Spec BUILD ghi rõ việc này thuộc LOCK (Feature Assignment); `02-lock.md` đã ghi là sẽ làm cùng BUILD |
| 2b | Đổi "kiểm tra GitHub OAuth Token" thành "kiểm tra App còn được cài" | **Đổi.** Khi dùng App, token của dev không quyết định ShipMate còn quyền với repo hay không. Giữ nguyên câu chữ sẽ báo động sai (dev thu hồi token) và bỏ sót (App bị gỡ) |
| 4 | Repo cá nhân (không thuộc Organization) | **Không bắt buộc Organization.** Hiện cảnh báo "không khóa cứng được", bật phát hiện push lén (GĐ 5) |
| 5 | AI so code với phạm vi nào | **Feature của chính dev.** Code lấn sang feature khác trong snapshot cũng là drift; feature đó vào `suspected_feature_ids` và người giữ nó được báo |
| 6 | CLI | **Repo này chỉ làm API.** Khi test, thay CLI bằng `git push` + curl |
| 7 | Nhánh git | **Merge PR #14 và PR LOCK vào `main` trước**, rồi tạo `feature/build` từ `main` |
| A | Bảng `audit_trail` dạng chuỗi hash dùng chung | **Làm trong GĐ 3.** SHIP neo Ship Card vào hash cuối của chuỗi này |
| B | LOCK ghi Lock và Feature Challenge vào `audit_trail` | **Làm.** Nếu không, lịch sử đổi scope nằm ngoài chuỗi và có thể bị sửa mà Ship Card không phát hiện được. Sửa code LOCK nên cần đồng ý |
| C | Đưa Check Run lên GĐ 5 | **Đưa lên.** SHIP lập luận rằng code trên production đã được merge gate của BUILD bảo vệ |

### Mặc định (đã được chấp nhận cùng các quyết định trên)

- Nhánh gốc để so diff: nhánh mặc định của repo.
- BUILD chỉ **ghi sự kiện** vào AuditTrail, không tính điểm. Trọng số điểm do SHIP chốt với thầy Vũ.
- Gọi AI 1 lần cho mỗi lần phân tích, trả cả scope lẫn gợi ý tinh gọn.
- Diff quá lớn thì báo `DIFF_TOO_LARGE`, không cắt bớt.
- Phân tích chạy nền; submit mới hủy lần cũ của cùng feature.
- Test có gọi AI thật hoặc GitHub thật thì hỏi trước mỗi đợt.

## 3. Hạ tầng GitHub đã chuẩn bị (2026-10-20)

| Thứ | Giá trị |
|---|---|
| GitHub App | `shipmate-bot-dev`, App ID `5390489`, cài được trên "Any account", webhook tắt |
| Quyền repo của App | Administration RW, Checks RW, Contents RW, Metadata R |
| Private key | Đã tạo, người phụ trách giữ file `.pem` ngoài thư mục dự án. Backend đọc theo đường dẫn `GitHubApp:PrivateKeyPath` (đặt trong `appsettings.Development.json`, file này đã bị gitignore). **Không commit** |
| Organization test | `shipmate-test-org` |
| Repo test | `shipmate-test-org/demo-app` (public, có `main`) |
| Đã cài App vào repo test chưa | Chưa; cài khi bắt đầu test GĐ 0 |

- **Setup URL** của App đang là `http://localhost:5140/api/github/app/installed`. Với cách B backend không cần endpoint này; nên đổi thành trang workspace của frontend khi frontend có trang đó.
- Khi deploy thật, tạo App thứ hai (`shipmate-bot`) với URL thật. Code không đổi, chỉ khác cấu hình.
- Người tạo Organization là Owner nên **vượt được** luật bảo vệ nhánh. Muốn test "dev bị chặn" cần một tài khoản GitHub thứ hai làm Member, hoặc bật "áp dụng cả cho admin".

## 4. Luồng tổng thể

```
Dev code xong ở local (nhánh feature/{featureId}-{username})
   │  shipmate submit
   ▼
CLI đẩy lên nhánh đệm staging/{featureId}-{username} (force-with-lease)
CLI gọi API ShipMate: "đã submit commit abc123"
   │
   ▼
[GĐ 1] Kiểm tra nhánh, người submit, trạng thái feature, App còn cài ── sai → từ chối
   │
   ▼
[GĐ 2] Lấy diff tích lũy (staging so với main)
       Lớp 1 Rule Engine: chỉ là bảo trì? ── đúng → bỏ qua AI, giữ kết quả cũ
       Lớp 2 AI: so diff với snapshot mới nhất → confidence_score
   │
   ├── điểm < ngưỡng ──► [GĐ 3] DRIFT: cảnh báo đỏ, khóa nút duyệt
   │                     Dev: sửa code rồi submit lại, hoặc gửi Feature Challenge (LOCK)
   │                     Manager: Override (ghi SCOPE_BYPASS vào AuditTrail)
   │
   └── điểm ≥ ngưỡng ──► IN-SCOPE: hiện gợi ý code gọn hơn
                         Dev: làm theo (sửa, submit lại) hoặc bỏ qua
   │
   ▼
[GĐ 4] Duyệt → Bot fast-forward feature/... tới đúng commit đã phân tích → xóa staging
[GĐ 5] Bot gắn Check Run ✅ lên commit đó → PR feature/* → main mới merge được
[GĐ 8] Thông báo real-time
```

### Bước nào làm ở đâu

| Việc | Ở đâu | Ai làm | Bằng cách nào |
|---|---|---|---|
| Cài App vào repo | Trang GitHub | Chủ repo | Bấm nút, 1 lần |
| Liên kết repo với workspace | Web ShipMate | Manager | Bấm nút (luồng cũ) |
| Nhận feature | Web ShipMate | Dev | Bấm nút; Bot tạo nhánh và khóa nhánh |
| Gửi code kiểm tra | Terminal máy dev | Dev | `shipmate submit` |
| Push staging + báo backend | Máy dev → GitHub → backend | CLI | `git push` + gọi API |
| Lấy diff, gọi AI | Server ShipMate | Backend | GitHub API + AI |
| Xem kết quả, Ack, Override, Duyệt | Web ShipMate | Dev / Manager | Bấm nút |
| Fast-forward, Check Run, xóa staging | Server ShipMate | Backend bằng Bot | GitHub API |

## 5. Các lớp rào

| # | Lớp rào | Chặn cái gì | Loại | GĐ |
|---|---|---|---|---|
| 1 | Branch Protection từ lúc nhận feature | Push thẳng vào `feature/...` | Chặn cứng | 0 |
| 2 | Kiểm tra nhánh và danh tính khi submit | Submit từ `main`, feature không tồn tại, nhánh người khác, feature đã bị loại, chưa nhận, backup | Chặn cứng | 1 |
| 3 | Governance Suspended khi App bị gỡ | ShipMate mất quyền với repo; SHIP bị chặn | Chặn cứng | 1 |
| 4 | Session Lock theo feature | Submit dồn dập làm lẫn kết quả | Chống lỗi | 2 |
| 5 | Rule Engine điều kiện kép | Giấu code dưới message "refactor" | Chống lách | 2 |
| 6 | Diff tích lũy so với `main` | Chia nhỏ hoặc commit đè để né | Chống lách | 2 |
| 7 | AI so với snapshot đã khóa | Code ngoài scope | Phát hiện | 2 |
| 8 | Khóa nút duyệt khi có drift; "Đã xem" không mở khóa | Xem cảnh báo cho có rồi duyệt | Chặn cứng | 3, 4 |
| 9 | Chỉ Manager Override, có ghi lại; solo bị gắn cờ | Tự phá rào thay vì đi Feature Challenge | Răn đe | 3 |
| 10 | Chỉ Bot fast-forward, đúng commit đã phân tích | Lén thêm code sau khi duyệt | Chặn cứng | 4 |
| 11 | Check Run bắt buộc trên PR vào `main` | Code chưa qua ShipMate lọt vào `main` | Chặn cứng | 5 |
| 12 | Phát hiện push lén (ngoài spec) | Lách trên repo cá nhân | Phát hiện | 5 |
| 13 | Khóa kép ngưỡng, hạ ngưỡng bị ghi lại | Manager hạ ngưỡng cho code lọt | Chống lách, răn đe | 6 |
| 14 | NBA (và VERIFY) bị chặn khi còn drift | Làm tiếp trên code sai scope | Chặn tiến độ | 7 |
| 15 | AuditTrail chuỗi hash, Ship Card neo vào | Xóa hoặc sửa dấu vết | Răn đe | 3 |

Spec dồn trọng tâm vào lớp 1 (khóa đầu vào) và lớp 11 (khóa đầu ra). Cả hai cần GitHub App.

## 6. Mô hình dữ liệu dự kiến

Chỉ thêm những gì các GĐ thật sự dùng.

### Cột mới trên bảng có sẵn

| Bảng | Cột | Dùng cho |
|---|---|---|
| `workspaces` | `github_installation_id` | Xin token của Bot cho repo này (GĐ 0) |
| `workspaces` | `github_repo_is_organization` | Repo thuộc Organization thì mới giới hạn được "chỉ Bot push"; FE dùng để cảnh báo repo cá nhân (GĐ 0) |
| `workspaces` | `governance_suspended_at`, `governance_suspended_reason` | Cờ Governance Suspended; SHIP đọc (GĐ 1) |
| `workspaces` | `last_audited_main_sha` | Phát hiện push lén vào `main` (GĐ 5) |
| `feature_assignments` | `last_approved_sha` | Commit cuối Bot đưa vào `feature/...`; dùng để phát hiện push lén (GĐ 4, 5) |

`DriftConfidenceThreshold` đã có sẵn trên `workspaces`. `BranchCreatedAt` chỉ được ghi khi **cả** tạo nhánh **và** bật Branch Protection đều thành công, nên không cần cột riêng cho Branch Protection.

### Bảng mới

**`build_submissions`** — mỗi lần submit một dòng.

| Cột | Ghi chú |
|---|---|
| `workspace_id`, `feature_id`, `submitted_by_user_id` | |
| `staging_branch`, `head_sha`, `base_sha` | Commit đã submit và commit `main` lúc lấy diff |
| `status` | `Analyzing`, `Superseded`, `InScope`, `Drift`, `SkippedMaintenance`, `Failed`, `Approved` |
| `snapshot_version`, `threshold_at_check` | |
| `confidence_score`, `suspected_feature_ids` | Kết quả AI |
| `findings`, `suggestions` (JSON) | Phần ngoài scope và gợi ý tinh gọn |
| `ai_analysis_run_id` | Liên kết log AI |
| `approved_sha`, `approved_by_user_id`, `approved_at` | GĐ 4 |

**`drift_alerts`**

| Cột | Ghi chú |
|---|---|
| `workspace_id`, `feature_id`, `submission_id` | |
| `snapshot_version_at_check`, `threshold_at_check`, `confidence_score`, `suspected_feature_ids` | Bất biến sau khi tạo |
| `status` | `Unresolved`, `Acknowledged`, `Overridden`, `Resolved` |
| `acknowledged_at/by`, `overridden_at/by`, `override_reason`, `resolved_at`, `resolved_by_submission_id` | |

**`audit_trail`** — chuỗi hash, dùng chung cho LOCK, BUILD, SHIP.

| Cột | Ghi chú |
|---|---|
| `workspace_id` | `chain_key` theo spec SHIP |
| `sequence` | 1, 2, 3…; unique `(workspace_id, sequence)` để chuỗi không rẽ nhánh |
| `audit_type` | `SNAPSHOT_LOCKED`, `SCOPE_CHANGE`, `SCOPE_BYPASS`, `SOLO_OVERRIDE_LOGGED`, `GOVERNANCE_DILUTION`, `THRESHOLD_CHANGED`, `SYSTEM_ALERT`, `DIRECT_PUSH_DETECTED` |
| `actor_user_id`, `payload` (JSON), `created_at` | |
| `prev_hash`, `record_hash` | `record_hash = SHA-256(prev_hash + sequence + audit_type + actor + payload + created_at)` |

- Code chỉ có hàm `Append`, không có Update hay Delete.
- Hai bản ghi ghi cùng lúc: một bản vi phạm unique index, đọc lại bản cuối rồi thử lại.
- Có hàm kiểm chứng: tính lại cả chuỗi, báo vị trí đầu tiên không khớp.
- Lịch sử đổi ngưỡng lấy từ các bản ghi `THRESHOLD_CHANGED`, không cần bảng riêng.

## 7. Chi tiết từng giai đoạn

### GĐ 0 — GitHub App và phần LOCK còn nợ

**Mục tiêu:** ShipMate thao tác với repo bằng Bot; nhánh `feature/...` bị khóa ngay khi dev nhận feature.

**Đã cài đặt (2026-10-23).** Các điểm khác với bản nháp ban đầu được ghi ngay trong từng bước.

**Cách làm:**
1. **Cấu hình:** `GitHubApp:AppId`, `GitHubApp:Slug` (`shipmate-bot-dev`, để tạo link cài App), `GitHubApp:PrivateKeyPath` (đường dẫn tới file `.pem`; dùng đường dẫn thay vì dán nội dung key vì PEM nhiều dòng khó đặt trong JSON).
2. **Xin token Bot:**
   - Ký JWT bằng private key (RS256, `iss` = App ID, hạn tối đa 10 phút).
   - Gọi `POST /app/installations/{installation_id}/access_tokens`, nhận token hạn 1 giờ.
   - Giữ token trong bộ nhớ đến gần hết hạn rồi xin lại.
3. **Liên kết repo (cách B):** trong `LinkGitHubRepoAsync`, sau khi kiểm tra Manager có quyền với repo:
   - Gọi `GET /repos/{owner}/{repo}/installation` bằng JWT của App.
   - Có thì lưu `github_installation_id`. Không có thì trả `GITHUB_APP_NOT_INSTALLED` kèm link `https://github.com/apps/{slug}/installations/new`.
   - Lưu thêm `github_repo_is_organization` và trả trong `WorkspaceDto` dưới tên `gitHubRepoIsOrganization`: `false` nghĩa là repo cá nhân, frontend hiện cảnh báo "không khóa cứng được". Lỗi `GITHUB_APP_NOT_INSTALLED` có `details.installUrl` để frontend dựng nút "Cài App".
   - Hủy liên kết thì xóa cả hai cột trên.
4. **Tạo và khóa nhánh khi nhận feature** (sửa `FeatureAssignmentService`):
   - Tạo nhánh bằng token Bot (`POST /repos/{o}/{r}/git/refs`).
   - Bật Branch Protection cho đúng nhánh đó (`PUT /repos/{o}/{r}/branches/{branch}/protection`): chỉ App được push, cấm force push, cấm xóa.
   - Repo cá nhân không hỗ trợ "chỉ App được push", nên chỉ bật cấm force push và cấm xóa. Nếu GitHub trả 403/404 khi bật luật trên repo cá nhân (gói không có Branch Protection) thì bỏ qua, vì repo đó vốn không khóa cứng được; với repo Organization, bật luật lỗi thì tính là lỗi tạo nhánh.
   - `BranchCreatedAt` chỉ ghi khi cả hai bước xong. Nút "Thử lại" (`RetryBranchAsync`) làm lại cả hai.
   - Workspace đã liên kết repo từ trước khi có App (chưa có installation) thì nhận feature chỉ lưu tên nhánh; Manager liên kết lại để có installation.
   - Đã bỏ `CreateBranchFromDefaultAsync` (token của dev) khỏi `IGitHubOAuthService`. Token OAuth của dev vẫn dùng để đăng nhập, lấy `github_username` và liệt kê repo.
5. Cập nhật `02-lock.md`, mục "Chưa làm" → xong Branch Protection.

**Chưa kiểm chứng trên GitHub thật:** xin token Bot, tìm installation, tạo nhánh, bật luật. Cần cài App vào `demo-app`, chạy migration và nạp cấu hình trước.

**Test:**
- Unit: token Bot hết hạn thì xin lại; liên kết repo chưa cài App trả lỗi đúng mã; nhận feature tạo nhánh và bật luật.
- GitHub thật (hỏi trước): cài App vào `demo-app`, liên kết, nhận feature, rồi thử `git push` thẳng vào `feature/...` bằng tài khoản Member và thấy bị từ chối.

**Xong khi:** dev nhận feature thì có nhánh bị khóa trên `demo-app`; push thẳng bị GitHub từ chối.

### GĐ 1 — Nhận submit và kiểm tra

**Mục tiêu:** chỉ submit hợp lệ mới vào phân tích.

**API:** `POST /api/workspaces/{id}/build/submissions` với `{ branchName, commitSha }`. Trả `202` kèm `{ submissionId, status: "Analyzing" }`.

**Kiểm tra theo thứ tự:**

| Tình huống | Kết quả |
|---|---|
| Workspace đang Governance Suspended | `GOVERNANCE_SUSPENDED` |
| Nhánh `main`, `develop`, `release/*` | `PROTECTED_BRANCH_SUBMIT`, gợi ý dùng hotfix |
| Nhánh `hotfix/*` | `HOTFIX_NOT_SUPPORTED` (hotfix để sau) |
| Tên nhánh sai mẫu, hoặc `featureId` không có trong snapshot mới nhất | `INVALID_FEATURE_ID` |
| Feature `replaced` | `FEATURE_REPLACED` |
| Feature `backup` | `FEATURE_IS_BACKUP` |
| Feature `locked`, chưa ai nhận | `FEATURE_NOT_ASSIGNED` |
| Feature `locked`, người khác đang giữ | `FEATURE_ASSIGNED_TO_OTHER` |
| Commit không có trên `staging/...` | `COMMIT_NOT_IN_STAGING` |

**Governance Suspended:** mỗi lần gọi GitHub bằng Bot mà GitHub báo App không còn được cài (404 khi xin token), thì:
- Bật cờ với lý do `APP_UNINSTALLED`.
- Ghi `SYSTEM_ALERT` vào AuditTrail.
- Gỡ cờ khi kiểm tra lại thấy App đã được cài.

**Xong khi:** mọi dòng trong bảng có unit test; submit hợp lệ tạo bản ghi `Analyzing`.

### GĐ 2 — Phân tích 2 lớp

**Lấy diff tích lũy:** `GET /repos/{o}/{r}/compare/{default_branch}...{head_sha}`. Bỏ qua file nhị phân. Vượt giới hạn kích thước (cấu hình, mặc định khoảng 60.000 ký tự diff), hoặc GitHub báo bị cắt, thì `DIFF_TOO_LARGE`.

**Lớp 1 — Rule Engine:**
- Lấy các commit mới từ lần phân tích trước của feature tới `head_sha`. Nếu commit cũ không còn trong lịch sử (dev đã force push) thì coi như có thay đổi logic.
- Bỏ qua AI khi **mọi** commit mới thỏa cả hai điều kiện:
  - Message khớp mẫu bảo trì: `fix typo`, `refactor`, `bump version`, `format code`, không phân biệt hoa thường.
  - Phần diff của chúng chỉ là whitespace, dòng comment, hoặc file `.gitignore`, `README*`, `LICENSE*`, `docs/**`.
- Kết quả: `SkippedMaintenance`, giữ nguyên kết quả và alert của lần trước. Không bao giờ dùng để đóng alert.

**Lớp 2 — AI** (một lần gọi qua `IAiGateway`, log vào `ai_analysis_runs`):
- Đầu vào: feature của dev (tên, mô tả, scope); danh sách feature khác trong snapshot (id, tên, mô tả, trạng thái); dependency; diff.
- Đầu ra (JSON Schema):
  ```json
  {
    "confidenceScore": 0.0,
    "suspectedFeatureIds": ["..."],
    "findings": [{ "file": "...", "description": "..." }],
    "suggestions": [{ "file": "...", "problem": "...", "suggestion": "...", "reason": "..." }]
  }
  ```
- Kiểm tra đầu ra như `DefineAnalysisOutputValidator`: điểm trong [0, 1], id phải có trong snapshot. Sai thì `Failed`, không tạo alert.
- `confidenceScore < threshold_at_check` thì `Drift` (tạo alert ở GĐ 3); ngược lại thì `InScope` (hiện gợi ý).

**Chạy nền và Session Lock:**
- `BackgroundService` đọc hàng đợi (Channel); mỗi submission chạy trong scope DI riêng.
- Giữ một `CancellationTokenSource` cho mỗi feature. Submit mới thì hủy lần đang chạy và đánh dấu nó `Superseded`.
- Khi lưu kết quả, kiểm tra lại: nếu submission đã `Superseded` thì bỏ kết quả.
- Feature khác nhau chạy song song.
- Giả định một instance server. Chạy nhiều instance thì trạng thái `Superseded` trong DB vẫn đảm bảo đúng, chỉ là không hủy được AI đang chạy ở instance khác.

**API:** `GET /api/workspaces/{id}/build/submissions/{sid}` để CLI và web xem kết quả.

**Test:** unit cho Rule Engine (đủ các tổ hợp message và diff), validator, Superseded. AI thật 1–2 lần (hỏi trước) trên `demo-app`: một diff đúng scope và một diff lệch scope.

### GĐ 3 — Drift alert, Ack, Override, AuditTrail

**Tạo alert:** submission `Drift` tạo `drift_alert` `Unresolved`.

**Chuyển trạng thái:**

| Từ | Sang | Ai | API |
|---|---|---|---|
| `Unresolved` | `Acknowledged` | Dev giữ feature | `POST .../build/alerts/{aid}/acknowledge` |
| `Unresolved`, `Acknowledged` | `Overridden` | Manager, bắt buộc có lý do | `POST .../build/alerts/{aid}/override` `{ reason }` |
| `Unresolved`, `Acknowledged` | `Resolved` | Tự động | Submit sau của cùng feature ra `InScope` với snapshot mới nhất |

- Override ghi `SCOPE_BYPASS` vào AuditTrail (alert id, lý do, người bấm). Nếu workspace chỉ có 1 thành viên active thì ghi thêm `SOLO_OVERRIDE_LOGGED`.
- `Resolved` bao gồm cả trường hợp dev đã sửa code và trường hợp Feature Challenge đã thêm phần đó vào scope (snapshot mới).
- `GET .../build/alerts?status=...` để web hiển thị.

**AuditTrail:** làm bảng và hàm `Append`/kiểm chứng ở GĐ này. Nếu chốt câu B thì thêm 2 điểm ghi ở LOCK:
- `SNAPSHOT_LOCKED` khi Lock lần đầu.
- `SCOPE_CHANGE` khi duyệt Feature Challenge (hành động, version mới).

**Test:** unit cho máy trạng thái (mọi chuyển hợp lệ và không hợp lệ); chuỗi hash (sửa một bản ghi thì kiểm chứng báo lỗi); hai lần Append đồng thời trên PostgreSQL thật không làm rẽ nhánh.

### GĐ 4 — Duyệt và fast-forward

**API:** `POST .../build/submissions/{sid}/approve` với `{ commitSha? }`.

**Điều kiện duyệt:**
- Submission là bản mới nhất của feature, đã phân tích xong (`InScope`, hoặc `Drift` mà alert đã `Overridden`).
- Feature không còn alert `Unresolved` hoặc `Acknowledged`.
- `commitSha` (nếu có, để chọn lọc commit) phải nằm trong lịch sử từ `last_approved_sha` tới `head_sha`. Mặc định là `head_sha`.

**Thực hiện bằng Bot:**
- `PATCH /repos/{o}/{r}/git/refs/heads/feature/...` với `{ sha, force: false }`. GitHub từ chối vì lệch nhánh thì trả `FAST_FORWARD_FAILED`, yêu cầu rebase và submit lại.
- Thành công thì:
  - Lưu `last_approved_sha`.
  - Xóa `staging/...` nếu đã chuyển tới đầu nhánh staging.
  - Đánh dấu submission `Approved`.

**Test:** unit cho điều kiện duyệt; GitHub thật (hỏi trước) trên `demo-app`: duyệt, thấy `feature/...` tiến tới đúng commit và staging bị xóa.

### GĐ 5 — Check Run và phát hiện push lén

**Merge gate:**
- Khi liên kết repo, Bot bật luật cho nhánh mặc định: bắt buộc check `ShipMate Scope Check`, chỉ chấp nhận check do App này tạo.
- Đọc luật đang có trước rồi gộp vào, **không ghi đè** các luật khác của repo.
- Khi fast-forward (GĐ 4), Bot tạo Check Run `ShipMate Scope Check` thành công trên commit đã duyệt.
- PR `feature/*` → `main` có ✅ nên merge được. PR khác thiếu ✅ nên bị khóa. Không cần webhook.

**Phát hiện push lén** (ngoài spec), chạy ở mỗi lần submit:
- `feature/...`: đầu nhánh khác `last_approved_sha` thì ghi `DIRECT_PUSH_DETECTED`.
- `main`: lấy commit mới từ `last_audited_main_sha`. Với mỗi commit, hỏi `GET /repos/{o}/{r}/commits/{sha}/pulls`. Không có PR, hoặc PR không có ✅ ShipMate, thì ghi `DIRECT_PUSH_DETECTED`. Sau đó cập nhật `last_audited_main_sha`.
- Kiểm tra theo PR nên xử lý được cả merge kiểu squash.

**Test:** GitHub thật (hỏi trước): mở PR từ nhánh lạ vào `main` thì bị khóa; PR từ `feature/...` đã duyệt thì merge được; push thẳng vào `main` trên repo cá nhân thì có bản ghi `DIRECT_PUSH_DETECTED`.

### GĐ 6 — Quản lý ngưỡng

**API:** `PUT /api/workspaces/{id}/drift-threshold` với `{ value }`. Chỉ Manager.

| Luật | Mã lỗi |
|---|---|
| Giá trị trong [0.70, 0.85]; sàn cứng hệ thống 0.65 | `THRESHOLD_OUT_OF_RANGE` |
| Tối đa 1 lần trong 7 ngày | `THRESHOLD_CHANGE_COOLDOWN` (kèm thời điểm được đổi tiếp) |
| Tối đa 3 lần cho cả dự án | `THRESHOLD_CHANGE_LIMIT_REACHED` |

- Mỗi lần đổi ghi `THRESHOLD_CHANGED`. Nếu hạ ngưỡng thì ghi thêm `GOVERNANCE_DILUTION`.
- Đếm số lần đổi từ AuditTrail. Không reset theo snapshot version.
- Alert cũ giữ `threshold_at_check`, không tính lại.

### GĐ 7 — NBA Engine

**API:** `GET /api/workspaces/{id}/next-best-action` cho dev hiện tại. Dùng luật, không gọi AI.

**Dữ liệu:** snapshot mới nhất (feature `locked`, `backup`, dependency), assignment, drift alert mở, submission mới nhất.

**Feature bị chặn khi** một feature tiền đề:
- đang `backup` → `dependency_pending_assignment`;
- có alert `Unresolved`/`Acknowledged` → `dependency_drift_unresolved`.

**Thứ tự ưu tiên** (spec không quy định):
1. Sửa drift của chính mình.
2. Duyệt submission đã sạch.
3. Làm feature của mình không bị chặn.
4. Nhận feature còn trống, vừa skill.

Trả về đúng 1 hành động, kèm `blockedReason` nếu feature của dev đang bị chặn. Không khóa các tác vụ khác.

`verify_checklist_incomplete` nối vào khi VERIFY có bảng checklist (xem mục 12).

BUILD cung cấp thêm hàm `HasOpenDriftAsync(workspaceId, snapshotVersion)` cho VERIFY dùng.

### GĐ 8 — SignalR Hub

- Hub `/hubs/workspace`, xác thực bằng JWT; mỗi workspace là một group.
- Event theo spec: `BranchPushSuccess`, `DriftAlertTriggered`, `DriftAlertAcknowledged`, `OptimizationSuggested`, `CodeScopeApproved`, `ManagerOverrideExecuted`.
- Thêm:
  - `BackupFeatureClaimed` để báo dev đang bị NBA chặn.
  - `AnalysisCompleted` để CLI và web biết khi nào có kết quả.
- Gộp 2 thông báo LOCK còn nợ: feature bị bỏ, và feature backup vừa với skill.

## 8. Mã lỗi mới

| Mã | HTTP | GĐ |
|---|---|---|
| `GITHUB_APP_NOT_INSTALLED` | 409 | 0 |
| `GOVERNANCE_SUSPENDED` | 409 | 1 |
| `PROTECTED_BRANCH_SUBMIT` | 400 | 1 |
| `HOTFIX_NOT_SUPPORTED` | 400 | 1 |
| `INVALID_FEATURE_ID` | 400 | 1 |
| `FEATURE_REPLACED` | 409 | 1 |
| `FEATURE_IS_BACKUP` | 409 | 1 |
| `FEATURE_NOT_ASSIGNED` | 409 | 1 |
| `FEATURE_ASSIGNED_TO_OTHER` | 403 | 1 |
| `COMMIT_NOT_IN_STAGING` | 400 | 1 |
| `GITHUB_REQUEST_FAILED` | 502 | 1 |
| `GITHUB_APP_UNINSTALLED` | 409 | 1 (nội bộ: kích hoạt Governance Suspended, không trả thẳng cho client khi submit) |
| `DIFF_TOO_LARGE` | 422 | 2 |
| `SUBMISSION_NOT_READY` | 409 | 4 |
| `SUBMISSION_SUPERSEDED` | 409 | 4 |
| `DRIFT_UNRESOLVED` | 409 | 4 |
| `FAST_FORWARD_FAILED` | 409 | 4 |
| `COMMIT_NOT_IN_SUBMISSION` | 400 | 4 |
| `INVALID_ALERT_TRANSITION` | 409 | 3 |
| `NOT_FEATURE_HOLDER` | 403 | 3 |
| `THRESHOLD_OUT_OF_RANGE` | 400 | 6 |
| `THRESHOLD_CHANGE_COOLDOWN` | 409 | 6 |
| `THRESHOLD_CHANGE_LIMIT_REACHED` | 409 | 6 |
| `THRESHOLD_UNCHANGED` | 400 | 6 |
| `THRESHOLD_CHANGE_CONFLICT` | 409 | 6 |

Mọi lỗi theo định dạng chung (`CONTRIBUTING.md` mục 9).

## 9. Liên quan tới LOCK, VERIFY, SHIP

| Bên | BUILD cần | BUILD cung cấp |
|---|---|---|
| LOCK | Snapshot mới nhất, assignment, dependency | Tạo và khóa nhánh khi nhận feature (GĐ 0); ghi Lock và Feature Challenge vào AuditTrail (nếu chốt câu B) |
| VERIFY (Lâm) | `all_items_checked` theo snapshot version, cho NBA | `HasOpenDriftAsync` để VERIFY chặn khi còn drift |
| SHIP (Phúc) | — | Cờ Governance Suspended; `drift_alerts` theo version và trạng thái; bản ghi `SCOPE_BYPASS`, `GOVERNANCE_DILUTION`, `DIRECT_PUSH_DETECTED` trong AuditTrail; `record_hash` cuối chuỗi để neo Ship Card; merge gate để SHIP tin rằng code trên production đã qua kiểm tra |

## 10. Giới hạn đã biết và cách bù

| Giới hạn | Cách bù |
|---|---|
| **Repo cá nhân không khóa cứng được** ("chỉ App được push" chỉ có trên repo của Organization), và chủ repo luôn tự tắt được luật | Phát hiện push lén vào `feature/...` và `main`, ghi `DIRECT_PUSH_DETECTED` vào AuditTrail; Check Run vẫn hiện ✅/❌ làm bằng chứng; khuyến nghị đặt repo trong Organization (freelancer: Organization của khách) |
| **Owner/admin Organization vượt được luật** | Bật "áp dụng cả cho admin" khi tạo luật; hành vi vượt luật vẫn bị phát hiện ở GĐ 5 |
| **Repo private trên gói Free không bật được luật bảo vệ** (cần xác minh khi làm GĐ 0) | Cảnh báo khi liên kết repo; vẫn có phát hiện push lén |
| **Người có quyền vào thẳng DB có thể tính lại cả chuỗi hash** | Ship Card công khai và giữ hash tại thời điểm mint, nên ghi đè sau đó vẫn bị phát hiện khi đối chiếu |
| **Chạy nhiều instance server** | Đúng nhờ trạng thái `Superseded` trong DB; chỉ không hủy được AI đang chạy ở instance khác |
| **AI có thể đánh giá sai** | Ngưỡng chỉnh được (có kiểm soát); dev có Feature Challenge, Manager có Override (bị ghi lại) |

## 11. Để sau

- Nhánh `hotfix/*`, Emergency Hotfix Override, `POST_INCIDENT_AUDIT_REQUIRED`, `PENALTY_REFUND`.
- `shipmate submit --emergency-bypass` và đối chiếu heartbeat (Fraudulent Bypass Detected).
- Tính điểm: việc của SHIP.
- Quét lại PR khi được mở (cần webhook và URL public).
- Trạng thái feature `deferred`: spec liệt kê nhưng LOCK chưa định nghĩa.
- ~~Endpoint xem lịch sử đổi ngưỡng~~: đã làm (2026-10-29), xem cuối mục GĐ 6.

## 12. Việc cần trao đổi với thành viên khác

**Gửi Phúc (SHIP)**
- Governance Suspended vẫn giữ, SHIP đọc như cũ. Điều kiện bật cờ đổi thành "App ShipMate bị gỡ khỏi repo", vì ShipMate ghi vào repo bằng Bot chứ không bằng OAuth token của dev. Lý do lưu kèm là `APP_UNINSTALLED`. Thông báo ở SHIP bước 3 nên là: "ShipMate đã mất quyền với repo. Vui lòng cài lại ShipMate App."
- BUILD ghi thêm `DIRECT_PUSH_DETECTED` (ngoài spec). Phúc quyết có đưa vào Scope Discipline Score không, và xếp vào loại nào.
- Ship Card neo vào `record_hash` cuối của `audit_trail` theo `workspace_id`.

**Gửi Lâm (VERIFY)**
- Spec BUILD ghi VERIFY bị chặn khi còn drift `unresolved`/`acknowledged`, nhưng spec VERIFY chưa nhắc. BUILD cung cấp `HasOpenDriftAsync(workspaceId, snapshotVersion)`. Đề xuất: không cho hoàn tất checklist khi còn drift mở.
- NBA cần đọc `all_items_checked` theo snapshot version; cần thống nhất tên bảng và field. Đề xuất cách hiểu `verify_checklist_incomplete`: NBA không chặn việc code. Khi mọi feature của version đã được duyệt mà checklist chưa xong, NBA gợi ý "Hoàn thành Verification Checklist v{n}", và hành động "Ship v{n}" bị chặn với lý do này.

**Gửi người viết spec BUILD (spec ghi tên Khoa)**
- Spec không quy định ShipMate Bot là gì; đã chọn GitHub App vì chỉ App mới tạo được Check Run.
- "Kiểm tra GitHub OAuth Token" được hiểu thành "kiểm tra App còn được cài".
