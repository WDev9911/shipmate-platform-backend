# ADMIN: kế hoạch cài đặt

Kế hoạch chi tiết cho module Administrator, viết trước khi code. Nguồn: mục 3.2, 3.3, 3.7 và FR-4.1 đến FR-4.7 của spec; quyết định của nhóm ngày 2026-11-06 trong [`docs/notes/thao-luan-admin-role-quota-context-diagram.md`](../notes/thao-luan-admin-role-quota-context-diagram.md); các quyết định ngày 2026-11-21 (mục 12).

**Đối chiếu với code:** `main` sau PR #18 (commit `cfcbd37`), rà 2026-11-20 và 2026-11-21. Mọi số dòng trong tài liệu này đúng với commit đó.

**Trạng thái (cập nhật 2026-11-23):** đây là **đợt 5** trong [kế hoạch tổng thể](00-ke-hoach-tong-the.md); một số bước của giai đoạn A làm sớm ở đợt 1 và đợt 2 (bảng ở mục 7.0).

| Giai đoạn | Trạng thái | Mức kiểm |
|---|---|---|
| A (tài khoản) | Đã code trên nhánh `feature/admin`, chưa commit | Đã chạy HTTP thật trên DB dev (lần 1: 34 ca; lần 2 bù: tìm kiếm có `_`, `accountLocked`, luật Admin không đổi trạng thái Admin). **Vẫn chỉ có unit test:** GitHub login với tài khoản bị khóa (A.1), refresh và xác minh email với tài khoản bị khóa, nội dung email khi khóa (A.12), seed với mật khẩu yếu hoặc email của Developer (A.17) |
| B (cấu hình nền tảng) | Đã code trên nhánh `feature/admin`, chưa commit. Migration `AddPlatformSettings` đã áp lên DB dev | Đã chạy HTTP thật (2026-11-23): quyền, kiểm đầu vào (12 ca), `revision` và `NO_CHANGE`, nhật ký từng trường, hai Admin lưu cùng lúc (4 lượt, lần nào cũng đúng 1 thắng, 1 nhận 409), endpoint thử mẫu, workspace mới chép đúng giá trị, 21 workspace cũ giữ 10. **Chỉ có unit test:** BUILD dùng mẫu từ cấu hình (cần GitHub thật), Lock và Feature Challenge dùng ngân sách của workspace (cần phân tích AI), chiều `Down` của migration |
| C (nhật ký gọi AI), E (giám sát) | Chưa làm | — |
| F (tài liệu) | Làm một phần: README (cách đặt `AdminSeed`) và [admin-test-report.md](../admin-test-report.md) đã xong. **Chưa làm:** `docs/frontend-guide.md` (hiện không có chữ nào về Admin), BM03 | — |

**Kết luận 2026-11-23: giai đoạn A và B đã xong phần code và phần kiểm, nhưng chưa đóng hẳn.** Còn các việc sau:
1. Commit và mở PR cho `feature/admin` (hiện 53 file đổi, chưa commit).
2. Cập nhật `docs/frontend-guide.md`: API mới (`action-logs`, `settings`, thử mẫu), API bỏ (`PATCH /users/{id}/role`), mã lỗi mới (`NO_CHANGE`, `ACCOUNT_REMOVED_FINAL`, `ADMIN_CANNOT_USE_WORKSPACES`, `ADMIN_ACCOUNT_PROTECTED`, `SETTINGS_CHANGED_BY_OTHER`), `accountLocked`, 401 khi tài khoản bị khóa giữa phiên, Admin không có màn hình workspace.
3. **Phần chạm vào LOCK và BUILD do bạn làm, nên bạn duyệt trước khi merge** (6 file, +51 −35): `LockGatePolicy`, `ChallengeRecommendationPolicy` (ngân sách thành tham số), `LockService`, `FeatureChallengeService` (giữ workspace để lấy ngân sách), `MaintenanceChangePolicy`, `BuildAnalysisService` (đọc mẫu commit từ cấu hình). Cũng chạm `WorkspaceAccessGuard` và `WorkspaceService` (chặn Admin, chép cấu hình khi tạo workspace).
4. **Các câu hỏi chưa được bạn chốt nhưng code đã làm theo đề xuất:** C1 (sàn và khoảng ngưỡng chỉ hiện), C3 (chỉ `accountLocked`), C5 (kiểm trạng thái mỗi request), C7 (`Removed` là cuối), C10 (email khi khóa). C8 (không tự gỡ khỏi repo GitHub) và C11 (email `Removed` bị giữ mãi) cũng đang theo đề xuất. Nếu quyết định khác thì phải sửa code.
5. Các phần chỉ có unit test (liệt kê ở hai dòng trên) vẫn chưa chạy thật; cần GitHub thật, AI thật hoặc thử trên giao diện.

Chưa kiểm ở đâu cả: chiều `Down` của hai migration, hiệu năng của truy vấn thêm mỗi request, giao diện frontend. Phần lớn code A được viết trước test, nên chưa chứng minh được từng test sẽ đỏ khi chưa sửa.

**Phạm vi đã rút gọn (2026-11-21):** chỉ giữ những gì cần để trình bày FR-4.1, 4.3, 4.4, 4.6, 4.7 với ít code nhất. Các phần tốn công mà giá trị demo thấp chuyển xuống **mục 13 (Làm sau nếu còn thời gian)**, giữ nguyên thiết kế để cần thì làm tiếp. Số bước của giai đoạn A giữ nguyên số cũ để các tham chiếu từ kế hoạch tổng thể vẫn đúng; bước nào đã chuyển xuống mục 13 thì ghi rõ.

Mục lục
1. Nguyên tắc và ranh giới
2. Hiện trạng
3. Lỗi tìm thấy trong code hiện tại
4. Phạm vi: làm, không làm
5. Dữ liệu mới và thay đổi
6. API
7. Giai đoạn và các bước
8. Mã lỗi mới
9. Bẫy kỹ thuật cần tránh
10. Ảnh hưởng tới luồng khác và frontend
11. Kiểm thử
12. Quyết định đã chốt và câu hỏi còn mở
13. Làm sau nếu còn thời gian
14. Lịch sử thay đổi của tài liệu

---

## 1. Nguyên tắc và ranh giới

1. **Admin không xem, không sửa nội dung workspace của người khác** (2026-11-06). Không có danh sách hay chi tiết workspace, không xem prompt hay câu trả lời AI, submission, drift alert, ngưỡng của từng workspace. Admin chỉ thấy **số tổng hợp**.
2. **Tài khoản Admin là tài khoản riêng, không dùng workspace** (2026-11-21, C6 và C12):
   - Tạo bằng seed lúc khởi động, không nâng từ Developer. Không có API đổi role.
   - Không tạo workspace, không được mời, không vào được workspace nào.
   - Ai cần làm dự án thì dùng một tài khoản Developer khác.
3. **Không có quota và subscription plan** (2026-11-06). Ghi vào BM03.
4. **Cấu hình của Admin không có hiệu lực ngược.** Giá trị được chép vào workspace lúc tạo, giống cách `threshold_at_check` và `dev_count_at_lock` đang làm. Admin đổi số thì chỉ workspace mới nhận số mới. Nếu không làm vậy, ngân sách của một snapshot đã Lock có thể "vượt" sau một lần Admin đổi số, và Feature Challenge sẽ tính sai.
5. **Mọi thao tác ghi của Admin vào nhật ký Admin** (`admin_action_logs`), tách khỏi `audit_trail` của workspace (spec 3.2: "Admin audit log riêng").
6. **YAGNI:** không thêm cache, feature flag, job dọn dẹp, cơ chế khóa hay thống kê khi chưa cần.

## 2. Hiện trạng

### 2.1. API Admin đang có

[AdminController.cs](../../ShipMate.API/Controllers/AdminController.cs) (`api/admin/users`, `[Authorize(Roles = "Administrator")]`), [AdminService.cs](../../ShipMate.Application/Services/AdminService.cs):

| Endpoint | Việc | Sau kế hoạch |
|---|---|---|
| `GET /api/admin/users?page&pageSize` | Danh sách user, sắp theo ngày tạo, không lọc, không tìm | Thêm tìm, lọc, kiểm tham số, `gitHubUsername` (A.5, A.11) |
| `PATCH /api/admin/users/{id}/status` | Khóa, mở, Removed; thu hồi refresh token khi không còn `Active` | Thêm chặn thao tác lên tài khoản Admin, `NO_CHANGE`, `Removed` là cuối, cắt hub, email (A.2b, A.4, A.7, A.10, A.12) |
| `PATCH /api/admin/users/{id}/role` | Đổi Developer hoặc Administrator | **Xóa** (A.3) |
| `GET /api/admin/users/{id}/activity` | Lịch sử đăng nhập và thao tác Admin lên user đó | Giữ nguyên |

- `AdminService` **chưa có unit test nào**. Chỉ có `UpdateUserRoleRequestValidatorTests.cs` (2 ca), sẽ xóa cùng API đổi role.
- Chưa có cách tạo Admin ngoài sửa `role = 'admin'` trực tiếp trong DB.

### 2.2. Nhật ký Admin

Bảng `admin_action_logs` ([AdminActionLogConfiguration.cs](../../ShipMate.Infrastructure/Persistence/Configurations/AdminActionLogConfiguration.cs)):
- `target_user_id` **bắt buộc**; entity khai `Guid TargetUserId` và `User TargetUser = null!`.
- `old_value`, `new_value` tối đa 50 ký tự; `action` tối đa 30 ký tự.
- Enum `AdminAction` (`StatusChanged`, `RoleChanged`) đổi sang chuỗi bằng từ điển `ActionToDb`; đọc ngược bằng `ActionToDb.First(...)`, nên một chuỗi lạ trong DB làm lỗi lúc đọc.
- Chưa có API đọc nhật ký này, trừ phần lọc theo user đích trong `/activity`.

### 2.3. Giá trị đang viết cứng mà spec muốn Admin chỉnh được

| Giá trị | Ở đâu | Dùng ở | Kế hoạch |
|---|---|---|---|
| Ngân sách 10 điểm/dev | [LockGatePolicy.cs:57](../../ShipMate.Domain/Policies/LockGatePolicy.cs:57) (`BudgetPerDeveloper`, dùng lại ở dòng 88) | `LockService.cs:162`, `FeatureChallengeService.cs:183`, `ChallengeRecommendationPolicy.cs:24` (qua `FeatureChallengeService.cs:344`) | Admin chỉnh (B) |
| Ngưỡng drift mặc định 0.75 | [Workspace.cs:36](../../ShipMate.Domain/Entities/Workspace.cs:36) và `HasDefaultValue(0.75m)` ở `WorkspaceConfiguration.cs:52` | Lúc tạo workspace | Admin chỉnh (B) |
| Sàn 0.65, khoảng Manager [0.70–0.85], 3 lần, 7 ngày | [DriftThresholdPolicy.cs](../../ShipMate.Domain/Policies/DriftThresholdPolicy.cs) (`HardFloor`, `Min`, `Max`, `MaxChanges`, `Cooldown`) | `DriftThresholdService` | Chỉ hiện (C1) |
| Mẫu commit bảo trì (`fix typo`, `refactor`, `bump version`, `format code`) | [MaintenanceChangePolicy.cs:11](../../ShipMate.Domain/Policies/MaintenanceChangePolicy.cs:11) | `BuildAnalysisService.cs:190` | Admin chỉnh (B) |
| 5 mục checklist VERIFY | [VerificationItemKeys.cs](../../ShipMate.Domain/Constants/VerificationItemKeys.cs) | `VerificationChecklist.CreateFor`, `VerificationService` (69 chỗ tham chiếu) | Giữ cố định (mục 13.1) |
| Model AI | `Gemini:Model` trong appsettings | `GeminiAiGateway` | Giữ trong appsettings |
| Prompt AI | File nhúng `Ai/*/Prompts/*.md`, `*.schema.json` | 4 nơi gọi AI | Giữ trong git |

### 2.4. Nơi gọi AI

`ProductDefinitionAnalysisService.cs:131` (DEFINE), `ComplexityScoringClient.cs:20` và `PersonaConflictClient.cs:43` (LOCK, Feature Challenge), `BuildAnalysisClient.cs:24` (BUILD). Cả 4 đi qua `IAiGateway.GenerateJsonAsync` ([GeminiAiGateway.cs](../../ShipMate.Infrastructure/Services/Ai/GeminiAiGateway.cs), đăng ký bằng `AddHttpClient`). Chỉ DEFINE ghi `AiAnalysisRun`; số token không lưu ở đâu cả (gateway bỏ qua `usageMetadata` của Gemini).

## 3. Lỗi tìm thấy trong code hiện tại

| # | Lỗi | Chỗ | Hậu quả | Xử lý |
|---|---|---|---|---|
| L1 | Đăng nhập GitHub **không kiểm tra `Status`** | `AuthService.GitHubLoginAsync` (`AuthService.cs:200`), ca 2 và 4 | Tài khoản bị khóa vẫn đăng nhập được bằng nút GitHub | A.1, đợt 1 |
| L2 | Refresh token không kiểm tra `Status` | `AuthService.RefreshTokenAsync` (`AuthService.cs:372`) | Token cấp qua L1 hoặc L3 vẫn được làm mới | A.1, đợt 1 |
| L3 | Xác minh email cấp token không kiểm tra `Status` | `AuthService.VerifyEmailAsync` (`AuthService.cs:115`) | Tài khoản chưa xác minh mà bị khóa vẫn nhận token | A.1, đợt 1 |
| L4 | Access token còn sống 30 phút sau khi bị khóa | Id và role nằm trong claim (`JwtTokenService.cs:34`), JWT không hỏi DB | Admin bị khóa vẫn gọi được `/api/admin/*`; user bị khóa vẫn thao tác | A.2, đợt 1 |
| L5 | Đổi role không thu hồi phiên | `AdminService.UpdateUserRoleAsync` | Token đang dùng vẫn mang role cũ tới khi hết hạn | **Không sửa: xóa API đổi role** (A.3), đợt 1 |
| L6 | Hai Admin khóa nhau cùng lúc thì không còn Admin nào | Chỉ chặn tự thao tác (`SelfActionNotAllowedException`) | Không còn ai mở khóa được; đã thử thật trên API (2026-11-22). Phép đếm "Admin cuối" không chặn được: người gọi luôn là Admin đang `Active` nên số Admin còn lại luôn ≥ 1 | A.4: không cho Admin đổi trạng thái của Admin khác |
| L7 | `page`, `pageSize` không được kiểm | `AdminController.GetUsers`, `UserRepository.GetPagedAsync` | `page=0` cho `Skip(-20)`, trả 500; `pageSize=1000000` trả cả bảng | A.5 |
| L8 | Mời được tài khoản đã khóa vào workspace | `WorkspaceService.AddMemberAsync` (`WorkspaceService.cs:166`) | Lời mời treo cho người không đăng nhập được | A.6 |
| L9 | Gateway biến việc hủy thành lỗi provider | `GeminiAiGateway.cs:57`, `catch (... TaskCanceledException)` | Tắt app giữa lúc phân tích thì submission thành `Failed` thay vì được chạy lại; số liệu lỗi AI cho Admin sai | C.1, đợt 2 |
| L10 | Kết nối SignalR đã mở thì sống mãi | `WorkspaceHub` chỉ kiểm JWT lúc kết nối | User bị khóa vẫn nhận sự kiện tới khi đóng tab. A.2 **không** sửa được lỗi này | A.2b, đợt 1 |
| L11 | Đăng nhập báo "bị khóa" trước khi kiểm mật khẩu | `AuthService.LoginAsync` (`AuthService.cs:182`) | Ai biết email cũng dò được tài khoản nào bị khóa | A.9, đợt 1 |

**Đã kiểm chứng, không phải lỗi:** `[Authorize(Roles = "Administrator")]` chạy đúng với cấu hình JWT hiện tại (`MapInboundClaims = false`, claim `ClaimTypes.Role`), đã thử bằng app riêng với cùng phiên bản gói. Chưa có test HTTP nào cho `/api/admin/*`, nên giai đoạn A vẫn chạy kiểm 401/403/200 thật.

**Không phải lỗi nhưng cần quyết định (C3):** user bị khóa vẫn được tính là thành viên `Active` trong ngân sách LOCK (`LockService.cs:155`), Skill Ceiling, Feature Challenge (`FeatureChallengeService.cs:457`) và xác định solo/team (`DriftAlertService.cs:101`), vì code chỉ nhìn trạng thái thành viên workspace, không nhìn trạng thái tài khoản. Nếu user đó là Manager thì workspace không còn ai duyệt.

## 4. Phạm vi: làm, không làm

| Spec | Quyết định | Lý do |
|---|---|---|
| FR-4.1 quản lý tài khoản | **Làm**: tìm và lọc, không cho Admin khóa Admin khác, sửa L1–L11, đọc nhật ký Admin, seed Admin, chặn Admin khỏi workspace | Phần đã có còn thiếu hoặc có lỗ hổng |
| FR-4.1 đổi role | **Bỏ**: xóa API đổi role | Tài khoản Admin là tài khoản riêng (C6) |
| FR-4.2 quota, plan | **Không làm** | Đã bỏ (2026-11-06) |
| FR-4.3 cấu hình toàn cục | **Làm**: ngân sách/dev, ngưỡng drift mặc định. Sàn 0.65, khoảng Manager, Dual-Lock **chỉ hiện** | Sàn nằm dưới mức thấp nhất Manager chọn được; sửa sàn kéo theo khoảng Manager và Dual-Lock (C1) |
| FR-4.3 strictness level | **Không làm** | Ngưỡng của Manager đã đóng vai trò này (C1) |
| FR-4.4 Rule Engine | **Làm**: danh sách mẫu commit bảo trì | Luật comment và khoảng trắng là thuật toán, không phải tham số |
| FR-4.4 "môi trường test/preview" | **Làm nhẹ**: endpoint thử commit với danh sách mẫu, không lưu | Rẻ, giúp Admin không đặt mẫu sai |
| FR-4.5 template VERIFY | **Không làm bây giờ**: giữ 5 mục cố định (mục 13.1) | Phải sửa code VERIFY của Lâm, có thể phá frontend; giá trị demo thấp |
| FR-4.6 AI gateway | **Làm gọn**: nhật ký mỗi lần gọi AI và số đếm theo ngày | Không lưu nội dung prompt hay câu trả lời. Phiên bản prompt, p95: mục 13.3 |
| FR-4.6 chọn model, sửa prompt trên web | **Không làm** | Prompt đi cùng schema và validator trong code; sửa trên web dễ làm hỏng cả 4 luồng AI |
| FR-4.7 giám sát | **Làm gọn**: một trang số đếm (user, workspace, BUILD, drift, phạt) | Độ trễ API, nhật ký lỗi: mục 13.4 |
| FR-4.7 Fraudulent Bypass | **Không làm** | Emergency bypass chưa có |
| 3.3 impersonate | **Không làm** | Vi phạm ranh giới |

## 5. Dữ liệu mới và thay đổi

Tổng cộng: **2 bảng mới** (`platform_settings`, `ai_call_logs`), **1 cột mới** (`workspaces.complexity_budget_per_developer`), **mở rộng 1 bảng** (`admin_action_logs`).

### 5.1. `platform_settings` (một dòng duy nhất, giai đoạn B)

| Cột | Kiểu | Mặc định | Ràng buộc |
|---|---|---|---|
| `id` | `int` | 1 | `CHECK (id = 1)` |
| `complexity_budget_per_developer` | `int` | 10 | 5–50 |
| `default_drift_threshold` | `numeric(4,2)` (cùng kiểu với `workspaces.drift_confidence_threshold`) | 0.75 | Trong [`DriftThresholdPolicy.Min`, `Max`] = [0.70, 0.85] |
| `maintenance_commit_patterns` | `text[]` | 4 mẫu hiện có | 1–30 mẫu; mỗi mẫu 3–50 ký tự sau khi trim và viết thường; không trùng |
| `revision` | `int` | 0 | Concurrency token (`IsConcurrencyToken()`) |
| `updated_at` | `timestamptz` | `null` | Nullable |
| `updated_by_user_id` | `uuid` | `null` | Nullable: lúc seed chưa có Admin nào sửa |

- Dùng cột có kiểu riêng chứ không dùng bảng key-value: chỉ có 3 giá trị, và cột có kiểu thì DB tự kiểm được.
- Chèn dòng `id = 1` bằng `HasData` trong configuration, không dùng `migrationBuilder.Sql`, để EF biết dòng này tồn tại.
- **Mọi giá trị trong `HasData` phải là hằng số**: `updated_at` và `updated_by_user_id` để `null`, không dùng `DateTime.UtcNow`. Nếu không, mỗi lần `migrations add` EF sinh thêm một lệnh `UpdateData`.
- Repository đọc mà không thấy dòng thì ném `InvalidOperationException` với thông báo rõ ràng; không tự tạo dòng mới, vì như thế sẽ âm thầm xóa cấu hình của Admin.

### 5.2. `workspaces.complexity_budget_per_developer` (cột mới, giai đoạn B)

- `int not null default 10`. Workspace cũ nhận 10, đúng với cách chúng đã được Lock.
- Entity: `public int ComplexityBudgetPerDeveloper { get; set; } = 10;`. **Bắt buộc có giá trị khởi tạo**: test tạo `Workspace` trực tiếp ở 6 chỗ (5 chỗ `new Workspace { ... }` trong `FeatureChallengeServiceTests`, `GitHubTokenRefreshTests`, `ProductDefinitionAnalysisServiceTests`, và `new() { ... }` trong `Scenarios/LockWorld.cs:23`). Thiếu giá trị này thì ngân sách bằng 0 và mọi test Lock, Feature Challenge báo vượt ngân sách.
- `WorkspaceService.CreateAsync` chép `complexity_budget_per_developer` và `default_drift_threshold` từ `platform_settings`, gán **tường minh** cả hai. Giữ `= 0.75m` ở entity cho test.
- `HasDefaultValue`: EF coi giá trị mặc định của kiểu (0, `0m`) là "chưa gán" và để DB tự điền. Giới hạn cấu hình (ngân sách ≥ 5, ngưỡng ≥ 0.70) bảo đảm giá trị chép vào không bao giờ bằng 0, nên EF luôn ghi đúng.
- `drift_confidence_threshold` đang là concurrency token, và `AppDbContext` đổi mọi lỗi concurrency của `Workspace` thành `ThresholdChangeConflictException`. Cột mới **không** là concurrency token, nên không ảnh hưởng.

### 5.3. `admin_action_logs` (mở rộng, giai đoạn A)

- `target_user_id` thành **nullable** (đổi cấu hình thì không có user đích). Entity: `Guid? TargetUserId`, `User? TargetUser`. `GetByTargetUserIdAsync` vẫn lọc được (`Guid? == Guid`).
- Thêm `subject varchar(50)`, ví dụ `complexity_budget_per_developer`.
- `old_value`, `new_value` lên `varchar(2000)` (danh sách mẫu commit dài hơn 50 ký tự).
- `AdminAction` thêm `SettingsChanged` → `settings_changed` (giai đoạn B). **Phải thêm vào `ActionToDb`**, nếu không lưu sẽ lỗi.
- **Giữ** `RoleChanged` / `role_changed` dù xóa API đổi role: DB có thể còn bản ghi cũ.

### 5.4. `ai_call_logs` (mới, giai đoạn C)

| Cột | Ghi chú |
|---|---|
| `id`, `created_at` | `created_at` có index |
| `purpose` | `define_analysis`, `complexity_scoring`, `persona_conflict`, `build_analysis` |
| `model` | `ModelVersion` provider trả về; lỗi trước khi có response thì dùng `_options.Model` |
| `outcome` | `succeeded`, `rate_limited`, `provider_unavailable`, `invalid_output`, `cancelled` |
| `duration_ms` | |
| `input_tokens`, `output_tokens`, `thinking_tokens` | Từ `usageMetadata`, nullable |

**Không lưu** workspace, user, prompt hay câu trả lời. `AiAnalysisRun` của DEFINE giữ nguyên.

## 6. API

Mọi route dưới `/api/admin`, `[Authorize(Roles = "Administrator")]`, cộng kiểm trạng thái tài khoản ở A.2. Tổng cộng **9 API**: 3 đang có, 6 mới.

| Method | Route | Việc | Giai đoạn |
|---|---|---|---|
| GET | `/users?page&pageSize&search&role&status` | `page ≥ 1`; `pageSize` 1–100 (mặc định 20); `search` ≤ 100 ký tự, tìm email hoặc tên (ILIKE, escape `%` `_`); trả thêm `gitHubUsername` | A |
| PATCH | `/users/{id}/status` | Như cũ, cộng: tài khoản Admin không đổi được (403), `NO_CHANGE`, `Removed` là cuối | A |
| ~~PATCH~~ | ~~`/users/{id}/role`~~ | **Xóa**, sau đó trả 404 | A |
| GET | `/users/{id}/activity` | Như cũ | — |
| GET | `/action-logs?page&pageSize&action&adminUserId` | Đọc nhật ký Admin, mới nhất trước | A |
| GET | `/settings` | Cấu hình hiện tại, kèm `revision` và các giới hạn cố định (sàn 0.65, khoảng Manager 0.70–0.85, số lần Manager được đổi ngưỡng và thời gian chờ giữa hai lần) dạng chỉ đọc. Dual-Lock không có hằng số nào trong code nên không hiện | B |
| PUT | `/settings` | Gửi đủ các trường cùng `revision`; sai revision thì 409 `SETTINGS_CHANGED_BY_OTHER` | B |
| POST | `/settings/maintenance-patterns/test` | `{ patterns?, commitMessages: [], files?: [{ path, patch }] }`. Trả từng message có khớp không; nếu có `files` thì cả commit có được bỏ qua AI không. Không lưu | B |
| GET | `/ai/usage?from&to` | Theo ngày và mục đích: số lần gọi, số lỗi theo `outcome`, tổng token, thời gian trung bình | C |
| GET | `/monitoring/overview` | Số đếm (7.E) | E |

Quy ước chung:
- `from`/`to` mặc định là 30 ngày gần nhất; khoảng tối đa 365 ngày; `from < to`; theo UTC.
- Mọi tham số query gom vào một class nhận bằng `[FromQuery]` để validator chạy (A.5).
- Endpoint thử mẫu: tối đa 100 file, tổng `patch` không quá `Build:MaxDiffCharacters` (60.000 ký tự, cùng giới hạn BUILD).

## 7. Giai đoạn và các bước

### 7.0. Thứ tự và phân đợt

Mỗi giai đoạn có migration riêng (nếu cần), test riêng và commit riêng. **A trước**, vì A mở rộng `admin_action_logs` mà B cần. Sau A, các giai đoạn B, C, E độc lập với nhau; F làm cuối. Giai đoạn D (template VERIFY) đã chuyển xuống mục 13.1.

| Bước | Làm ở |
|---|---|
| A.1, A.1b, A.2, A.2b, A.3, A.9 | **Đợt 1** (bảo mật tài khoản) |
| C.1 (L9) | **Đợt 2** (toàn vẹn workspace) |
| Các bước còn lại | **Đợt 5** (Admin) |

**Migration khi nhóm làm song song:** VERIFY và SHIP cũng thêm migration. Hai nhánh cùng sửa `AppDbContextModelSnapshot.cs` thì merge sẽ xung đột hoặc lệch model. Trước khi merge, rebase lên `main` mới nhất, xóa migration của mình rồi tạo lại bằng `dotnet ef migrations add`; không sửa tay file snapshot.

### A. Tài khoản (FR-4.1)

**A.1. Kiểm trạng thái tài khoản ở mọi chỗ cấp token (L1–L3).** Đợt 1.
- `GitHubLoginAsync`, `RefreshTokenAsync`, `VerifyEmailAsync`: `user.Status != Active` thì ném `AccountLockedException` (403 `ACCOUNT_LOCKED`, đã có).
- Trong `GitHubLoginAsync`, đặt bước kiểm **ngay sau khi tìm được user**, trước mọi thay đổi:
  - Ca 2 (gắn GitHub vào tài khoản email có sẵn): kiểm `existingByEmail` trước khi gán `GitHubId`, `SaveChangesAsync` và gửi email "đã liên kết".
  - Ca 4 (user GitHub quay lại): kiểm trước khi cập nhật `GitHubUsername` và avatar.
  - Kiểm muộn thì tài khoản bị khóa vẫn bị gắn GitHub, nhận email, và được lưu `github_connections`.

**A.1b. Quên mật khẩu.** Đợt 1. `ForgotPasswordAsync`: tài khoản không `Active` thì `return` im lặng, giống nhánh email không tồn tại. `VerifyResetCodeAsync` và `ResetPasswordAsync` không cần sửa: đặt lại mật khẩu không cấp token và còn thu hồi phiên.

**A.2. Kiểm trạng thái tài khoản mỗi request (L4, C5).** Đợt 1.
- Thêm `OnTokenValidated` vào `JwtBearerEvents` ở `Program.cs` (hiện chỉ có `OnMessageReceived`).
- Đọc `status` của user theo khóa chính; không `Active` thì `context.Fail(...)`, request nhận 401.
- Không cần so `role` với claim: sau A.3 không còn API đổi role.
- Lấy `IUserRepository` từ `context.HttpContext.RequestServices` (scoped).
- Với SignalR, việc này chỉ chặn **kết nối mới**; kết nối đang mở xử lý ở A.2b.
- Giá: thêm một truy vấn theo khóa chính mỗi request.

**A.2b. Cắt kết nối SignalR khi khóa (L10).** Đợt 1.
- Thêm `HubConnectionRegistry` (singleton): `OnConnectedAsync` ghi `Context` theo user, thêm `OnDisconnectedAsync` để xóa (hub hiện chưa có hàm này).
- Thêm `DisconnectUserAsync(Guid userId)` vào `IWorkspaceNotifier`; `SignalRWorkspaceNotifier` gọi `Context.Abort()` cho mọi kết nối của user đó.
- `AdminService.UpdateUserStatusAsync` gọi hàm này khi trạng thái mới khác `Active`, **sau** `SaveChangesAsync`.
- Kế hoạch tổng thể dùng chung registry này cho S2 (đưa thành viên bị xóa ra khỏi group workspace).
- Chỉ đúng khi chạy 1 instance, cùng giả định với SignalR hiện tại. "Token hết hạn mà kết nối vẫn sống" là giới hạn chung của SignalR; chỉ ghi vào tài liệu.

**A.3. Xóa API đổi role.** Đợt 1, thay cho việc sửa L5.
- Xóa: `PATCH /api/admin/users/{id}/role` trong `AdminController`, `IAdminService.UpdateUserRoleAsync`, `AdminService.UpdateUserRoleAsync`, `DTOs/Admin/UpdateUserRoleRequest.cs`, `Validators/Admin/UpdateUserRoleRequestValidator.cs`, `ShipMate.Tests/Application/UpdateUserRoleRequestValidatorTests.cs`.
- **Giữ** `AdminAction.RoleChanged` và dòng `role_changed` trong `ActionToDb`.
- Báo frontend: API này không còn, màn hình Admin bỏ nút đổi role.

**A.4. Admin không đổi trạng thái của Admin (L6).** Đã chốt 2026-11-22, thay cho phép đếm "Admin cuối".
- `AdminService.UpdateUserStatusAsync`: tài khoản đích có `Role = Administrator` thì 403 `ADMIN_ACCOUNT_PROTECTED`, trước mọi kiểm tra khác. Không khóa, không mở, không `Removed`.
- **Vì sao không đếm:** đã thử thật trên API. Người gọi luôn là Admin đang `Active` (A.2 chặn token của tài khoản bị khóa), mà phép đếm tính cả người gọi, nên số Admin còn lại luôn ≥ 1 và `LAST_ADMINISTRATOR` không bao giờ trả về. Hai Admin khóa nhau cùng lúc thì cả hai cùng thành công, không còn Admin nào (xảy ra ở lượt thử thứ 2). Bỏ `CountActiveAdministratorsExceptAsync` và `LastAdministratorException`.
- Hợp với việc Admin là tài khoản riêng do seed tạo (A.17): muốn khóa một Admin thì xử lý trong DB, muốn thêm Admin thì đổi `AdminSeed:Email`. Admin tự đổi trạng thái của mình vẫn bị `SELF_ACTION_NOT_ALLOWED` chặn trước.
- Cách dùng khóa DB để vẫn cho Admin khóa Admin: mục 13.6.

**A.5. Kiểm tham số danh sách user (L7).**
- Thêm `DTOs/Admin/AdminUserQuery.cs` (`Page = 1`, `PageSize = 20`, `Search`, `Role`, `Status`) nhận bằng `[FromQuery]`, và `Validators/Admin/AdminUserQueryValidator.cs`: `Page ≥ 1`, `PageSize` 1–100, `Search` ≤ 100, `Role` và `Status` `IsInEnum` (chặn cả `1`, giá trị Manager cũ).
- **Phải gom vào class**: `ValidationFilter` tìm validator theo kiểu của từng tham số, nên với `int page, int pageSize` rời như hiện nay thì không validator nào chạy. Áp dụng cho mọi endpoint có query: `/action-logs`, `/ai/usage`.
- `IUserRepository.GetPagedAsync(AdminUserQuery query)`: lọc và đếm trong SQL.

**A.6. Không mời được tài khoản không `Active` hoặc tài khoản Admin (L8, C12).** `AddMemberAsync`: user được mời không `Active`, hoặc là `Administrator`, thì 404 như khi không tìm thấy user, để không lộ trạng thái hay vai trò tài khoản cho Manager.

**A.7. `NO_CHANGE`.** Đổi trạng thái sang đúng giá trị đang có thì 400 `NO_CHANGE`, không ghi log, không thu hồi phiên.

**A.8. Đọc nhật ký Admin.**
- Migration `ExtendAdminActionLogs` theo 5.3.
- `GET /action-logs`; `IAdminActionLogRepository.GetPagedAsync(query)`, `Include` Admin và user đích.
- DTO: `id`, `adminUserId`, `adminEmail`, `adminDisplayName`, `targetUserId?`, `targetEmail?`, `action`, `subject?`, `oldValue`, `newValue`, `createdAt`.

**A.9. Đổi thứ tự kiểm ở đăng nhập (L11).** Đợt 1. Trong `LoginAsync`, đưa 2 bước kiểm `Status` và `IsEmailVerified` xuống sau bước kiểm mật khẩu. Sai mật khẩu thì luôn `INVALID_CREDENTIALS`, dù tài khoản bị khóa.

**A.10. `Removed` là trạng thái cuối (C7).** `Locked` mở lại được; `Removed` không chuyển sang trạng thái khác: 409 `ACCOUNT_REMOVED_FINAL`.

**A.11. Danh sách user trả thêm `gitHubUsername`.** Thêm vào `AdminUserListItemDto`; AutoMapper tự map vì entity đã có trường này. `lastActiveAt` chuyển xuống mục 13.7.

**A.12. Email khi bị khóa (C10).** Khóa hoặc `Removed` thì gửi email bằng `TrySendAsync` (gửi lỗi không làm hỏng thao tác), bất kể `NotificationPreference`. Mở khóa không gửi.

**A.13. Buộc đăng xuất.** Chuyển xuống mục 13.5.

**A.14. Cho Manager thấy thành viên bị khóa (C3, C8).** `WorkspaceMemberDto` thêm `accountLocked` (user không `Active`). Đây là thay đổi ở API workspace, Admin không thấy. Phần "tự chuyển Manager khi Manager bị khóa" chuyển xuống mục 13.8.

**A.15. Controller.** Giữ `AdminController` (`api/admin/users`, không đổi route). Thêm 2 controller, cùng `[Authorize(Roles = "Administrator")]`:
- `AdminSettingsController` (`api/admin/settings`): giai đoạn B.
- `AdminReportsController` (`api/admin`): `action-logs`, `ai/usage`, `monitoring/overview`.

**A.16. Admin không dùng workspace (C12).** Vì không còn đổi role (A.3) và Admin tạo bằng seed (A.17), một tài khoản Admin không bao giờ là thành viên workspace. Chỉ chặn ở 3 chỗ:

| Chỗ | Cách chặn |
|---|---|
| `WorkspaceAccessGuard.GetWorkspaceWithMembershipOrThrow` | `membership.User.Role == Administrator` thì `NotFoundException`, như khi không phải thành viên. Đây là hàm phụ duy nhất của guard và đã `Include(m => m.User)`, nên không tốn thêm truy vấn. Chặn cả 48 chỗ gọi guard, kể cả workspace đã lưu trữ. Phòng dữ liệu cũ và tài khoản bị sửa tay trong DB |
| `WorkspaceService.CreateAsync` | Người gọi là Admin (đọc qua `_userRepository`, đã có) thì 403 `ADMIN_CANNOT_USE_WORKSPACES` |
| `WorkspaceService.AddMemberAsync` | Gộp với A.6 |

- Lấy role từ DB, không từ claim.
- Không chặn ở nhận lời mời, danh sách workspace, danh sách lời mời và hub: Admin không bao giờ được mời (A.6), và có lỡ là thành viên thì guard đã chặn. Thiết kế chặn thêm giữ ở mục 13.9.
- Trước khi bật luật, chạy một truy vấn trên DB dev xem có tài khoản `admin` nào đang là thành viên workspace không. Guard đã chặn họ; nếu họ là Manager của workspace nào thì workspace đó cần xử lý tay.

**A.17. Seed tài khoản Admin lúc khởi động (C6).**
- Cấu hình `AdminSeed:Email`, `AdminSeed:Password`, `AdminSeed:DisplayName`, đặt bằng biến môi trường hoặc user-secrets. **Không** ghi vào `appsettings.json` (cùng lý do với S3). Thêm mục `AdminSeed` với giá trị trống vào `appsettings.Development.example.json`, ghi cách đặt vào README.
- `Services/AdministratorSeeder.cs` (Application), dùng `IUserRepository` và `IPasswordHasher` có sẵn. Gọi một lần trong `Program.cs` ngay sau `var app = builder.Build();`, trong `app.Services.CreateScope()`.
- App không chạy migration lúc khởi động, nên seeder chỉ chạy đúng khi DB đã ở migration mới nhất.

| Trường hợp | Xử lý |
|---|---|
| Thiếu `Email` hoặc `Password` | Bỏ qua, log Information |
| Mật khẩu không đạt luật đăng ký (độ dài, chữ hoa, chữ số như `RegisterRequestValidator`) | Log Error, không tạo |
| Chưa có user với email đó | Tạo `Role = Administrator`, `Status = Active`, `IsEmailVerified = true`, `EmailVerifiedAt = now`, `HasPassword = true`, mật khẩu hash bằng `IPasswordHasher`, `DisplayName` mặc định `Administrator` nếu trống |
| Đã có và là Administrator | Không làm gì; **không** đặt lại mật khẩu (Admin có thể đã đổi qua "quên mật khẩu") |
| Đã có và là Developer | Log Error, **không** đổi role (đổi là nâng quyền, trái A.3) |

- Muốn thêm Admin: đổi `AdminSeed:Email` rồi khởi động lại; Admin cũ vẫn còn. Không làm API tạo Admin.
- Luật mật khẩu: tách phần kiểm mật khẩu của `RegisterRequestValidator` ra một chỗ dùng chung, không chép lại.
- `RegisterAsync` và GitHub login (ca 1) vẫn luôn tạo `Developer`, không đổi.

**File của giai đoạn A:** `AuthService.cs`, `AdminService.cs`, `IAdminService.cs`, `AdminController.cs`, `AdminReportsController.cs` (mới), `WorkspaceService.cs`, `WorkspaceAccessGuard.cs`, `WorkspaceHub.cs`, `IWorkspaceNotifier.cs`, `SignalRWorkspaceNotifier.cs`, `Program.cs`, `UserRepository.cs`, `IUserRepository.cs`, `AdminActionLogRepository.cs`, `AdminActionLog.cs`, `AdminActionLogConfiguration.cs`, `WorkspaceMemberDto.cs`, `DTOs/Admin/*`, `Validators/Admin/*`, `Exceptions/AdminExceptions.cs` (mới), `AdministratorSeeder.cs` (mới), `appsettings.Development.example.json`, README, migration `ExtendAdminActionLogs`.

**Xong khi:** mọi ca test A ở mục 11 đạt; chạy HTTP thật được 200/403/401 cho `/api/admin/*`; khởi động với `AdminSeed:*` thì đăng nhập được Admin; `PATCH /users/{id}/role` trả 404.

### B. Cấu hình nền tảng và Rule Engine (FR-4.3, FR-4.4)

**B.1. Dữ liệu.** Entity `PlatformSettings`, configuration, `IPlatformSettingsRepository`, migration `AddPlatformSettings` (bảng 5.1 cộng cột 5.2).

**B.2. Chép vào workspace mới.** `WorkspaceService.CreateAsync` đọc `platform_settings` và chép 2 giá trị.

**B.3. Ngân sách đọc từ workspace.**
- `LockGatePolicy.Evaluate(features, skills, budgetPerDeveloper)`, `ChallengeRecommendationPolicy.Recommend(..., budgetPerDeveloper)`; xóa hằng `BudgetPerDeveloper`.
- Ở cả 3 nơi gọi, entity workspace **chưa có sẵn** ở chỗ gọi, nhưng guard đã trả về nó rồi bị bỏ đi:
  - `LockService.cs:162` nằm trong hàm phụ `EvaluateAsync(productDefinition)`: thêm tham số ngân sách; `CheckAsync` (dòng 42) giữ lại workspace mà `GetWorkspaceAsMemberAsync` trả về; `LockAsync` (dòng 51) đã giữ.
  - `FeatureChallengeService.cs:344` nằm trong `ToDtoAsync(challenge, snapshot)`, được gọi ở 6 chỗ: thêm tham số ngân sách; ở mỗi hàm public giữ lại workspace mà guard trả về. Không truy vấn workspace lại trong `ToDtoAsync`.
  - `FeatureChallengeService.cs:183`: đã giữ `workspace` (dòng 144).
- Không thêm `complexityBudgetPerDeveloper` vào `WorkspaceDto` (YAGNI): frontend đọc tổng ngân sách qua `GET /lock/check` (`LockGateDto.Budget`), và `LockGateFailedException` đã mang sẵn `LockGateDto`.

**B.4. Mẫu commit đọc từ cấu hình.** `MaintenanceChangePolicy.IsMaintenanceOnly(messages, files, patterns)`; `BuildAnalysisService` đọc danh sách mẫu một lần cho mỗi lần phân tích.

**B.5. `PlatformSettingsService`.**
- `GetAsync`, `UpdateAsync`, `TestPatternsAsync`.
- `UpdateAsync`: validator kiểm giới hạn 5.1; so `request.Revision` với giá trị vừa đọc (mục 9, ý 8); ghi một dòng `SettingsChanged` cho **mỗi** trường đổi, với `subject` là tên trường; tăng `Revision`, gán `UpdatedAt`, `UpdatedByUserId`.
- Thêm nhánh `catch (DbUpdateConcurrencyException ex) when (ex.Entries.Any(e => e.Entity is PlatformSettings))` vào `AppDbContext.SaveChangesAsync`.
- `TestPatternsAsync`:
  - Đổi `IsMaintenanceMessage` thành `public` (thêm tham số `patterns`), không viết lại phần so khớp ở chỗ khác.
  - Trả `messages: [{ message, matches }]` và, nếu có `files`, `skippedByRuleEngine` (gọi thẳng `IsMaintenanceOnly`).
  - Tài liệu của endpoint ghi rõ: message khớp khi **chứa** một mẫu (`Contains`, không phân biệt hoa thường), nên mẫu ngắn như `fix` khớp mọi message có chữ "fix"; commit chỉ được bỏ qua AI khi mọi message khớp **và** mọi file là config, tài liệu hoặc chỉ đổi khoảng trắng, comment.
  - Không đổi cách so khớp `Contains` (đổi là đổi hành vi BUILD, phần code của bạn).

**Test cần sửa:** `LockGatePolicyTests`, `FeatureChallengePolicyTests`, `MaintenanceChangePolicyTests`, `LockServiceTests`, `FeatureChallengeServiceTests`, `BuildAnalysisServiceTests` (fake repository phải trả cấu hình), `Scenarios/LockWorld.cs`.

**Xong khi:** workspace tạo sau khi đổi ngân sách nhận số mới, workspace cũ giữ số cũ; Lock với ngân sách 15/dev đúng; mẫu commit mới được BUILD dùng.

### C. Nhật ký gọi AI (FR-4.6)

**C.1. Hủy không thành lỗi provider (L9).** Đợt 2 (D3 trong kế hoạch tổng thể). Trong `GeminiAiGateway`, nếu `cancellationToken.IsCancellationRequested` thì ném lại `OperationCanceledException`, không đổi thành `AiProviderUnavailableException`. `TaskCanceledException` do HttpClient hết thời gian chờ (không phải hủy) vẫn là `provider_unavailable`. Nếu đợt 2 đã làm thì chỉ cần ghi `outcome = cancelled`.

**C.2. Mục đích của mỗi lần gọi.** `AiJsonRequest` thêm `Purpose` (bắt buộc); sửa 4 nơi gọi.

**C.3. Số token.** Đọc `usageMetadata` (`promptTokenCount`, `candidatesTokenCount`, `thoughtsTokenCount`) vào `GeminiApiPayloads.cs`.

**C.4. Ghi nhật ký.**
- Dùng **scope DI riêng** (`IServiceScopeFactory`, `DbContext` riêng, `SaveChanges` ngay), bọc `try/catch` và chỉ log warning khi lỗi. Không dùng `DbContext` của nơi gọi (mục 9, ý 2).
- Ghi trong `finally` của `GenerateJsonAsync`, để mọi lối ra đều có một dòng:

| Lối ra | `outcome` |
|---|---|
| Trả JSON | `succeeded` |
| HTTP 429 | `rate_limited` |
| Lỗi mạng, hết thời gian chờ, HTTP lỗi khác, `JsonException` khi đọc body (hiện gateway không bắt lỗi này) | `provider_unavailable` |
| `FinishReason` khác `STOP` hoặc JSON rỗng (`AiOutputInvalidException`) | `invalid_output` |
| Bị hủy (C.1) | `cancelled` |

**C.5. `GET /ai/usage`.** `GroupBy` theo ngày (`date_trunc('day', created_at)`) và `purpose`: số lần gọi, số lần theo từng `outcome`, tổng token, thời gian trung bình. Không có p95 (mục 13.3).

**Giới hạn:** `succeeded` nghĩa là provider trả JSON. Việc validator của từng luồng từ chối JSON thì không ghi ở đây (DEFINE ghi vào `AiAnalysisRun`, BUILD ghi vào `FailureCode` của submission).

**Xong khi:** mỗi `outcome` có một ca test; lỗi ghi nhật ký không làm hỏng lời gọi AI; `/ai/usage` trả đúng số trên dữ liệu giả.

### D. Template VERIFY

Không làm bây giờ. Thiết kế giữ ở mục 13.1.

### E. Giám sát (FR-4.7)

**E.1. `GET /monitoring/overview`** chỉ trả số đếm, mọi phép đếm bằng `GroupBy` + `Count` trong SQL:
- User theo role và trạng thái; số đăng ký mới trong 30 ngày gần nhất.
- Workspace theo trạng thái.
- BUILD: submission theo trạng thái; số đang `Analyzing` quá 10 phút (bị kẹt).
- Drift alert theo trạng thái.
- Số bản ghi `audit_trail` theo loại: `GOVERNANCE_DILUTION`, `SCOPE_BYPASS`, `SOLO_OVERRIDE_LOGGED`, `DIRECT_PUSH_DETECTED`, `SYSTEM_ALERT`.

Không đo "độ dài hàng đợi": `BuildAnalysisWorker` lấy mục ra khỏi channel ngay và chạy bằng `Task.Run`, nên `Reader.Count` gần như luôn bằng 0. Số đang `Analyzing` trong DB là con số đúng.

**Xong khi:** số đếm đúng trên dữ liệu giả.

### F. Tài liệu

- `docs/frontend-guide.md`: API Admin; mã lỗi mới (mục 8); `PATCH /users/{id}/role` không còn; Admin không có màn hình workspace; `accountLocked` trong danh sách thành viên; ngân sách trong `LockGateDto.Budget` không còn luôn bằng 10 × số dev; access token bị từ chối khi tài khoản bị khóa (A.2).
- README: cách đặt `AdminSeed:*`.
- BM03: bỏ quota, plan, strictness level, chọn model và sửa prompt trên web; giữ 5 mục VERIFY cố định; tài khoản Admin là tài khoản riêng, không dùng workspace.
- `docs/admin-test-report.md`: đã viết cho giai đoạn A và B (2026-11-23), cập nhật thêm khi làm C và E. Script kiểm HTTP không đưa vào repo (quyết định 2026-11-23, theo YAGNI); báo cáo là nơi lưu những gì đã chạy.

## 8. Mã lỗi mới

| Mã | HTTP | Khi nào | Bước |
|---|---|---|---|
| `ADMIN_ACCOUNT_PROTECTED` | 403 | Đổi trạng thái của một tài khoản Admin | A.4 |
| `NO_CHANGE` | 400 | Đổi trạng thái sang đúng giá trị đang có | A.7 |
| `ACCOUNT_REMOVED_FINAL` | 409 | Đổi trạng thái của tài khoản `Removed` | A.10 |
| `ADMIN_CANNOT_USE_WORKSPACES` | 403 | Admin tạo workspace | A.16 |
| `SETTINGS_CHANGED_BY_OTHER` | 409 | `revision` gửi lên khác giá trị hiện tại | B.5 |

Đã có, dùng lại: `ACCOUNT_LOCKED` (403), `INVALID_CREDENTIALS`, `SELF_ACTION_NOT_ALLOWED` (400). Lỗi validator trả 400 như các API khác. Các exception mới đặt trong `Exceptions/AdminExceptions.cs`, kế thừa `AppException`.

## 9. Bẫy kỹ thuật cần tránh

1. **Enum mới phải thêm vào từ điển chuyển đổi** (`ActionToDb`, tương tự `RoleToDb`), nếu không lưu sẽ lỗi 500. Không xóa giá trị cũ, nếu không đọc sẽ lỗi.
2. **Nhật ký AI không dùng chung `DbContext` với nơi gọi.** Dùng chung thì: nơi gọi ném lỗi trước `SaveChanges` làm mất bản ghi; nơi gọi bị lỗi concurrency (ví dụ `SnapshotVersionConflictException`) làm hỏng luôn lần ghi; bản ghi có thể bị lưu cùng một transaction nghiệp vụ không liên quan.
3. **Ghi nhật ký không bao giờ làm hỏng nghiệp vụ**: lỗi ghi chỉ log warning, giống `TrySendAsync`.
4. **Không có hiệu lực ngược**: đổi cấu hình không chạm workspace hay snapshot đã có. Có test riêng.
5. **`decimal` của ngưỡng**: so với `DriftThresholdPolicy.Min/Max` bằng `decimal`; validator từ chối hơn 2 chữ số thập phân (0.755 → 400), không để cột `numeric(4,2)` tự làm tròn.
6. **`text[]` của Npgsql**: chuẩn hóa (trim, viết thường, bỏ trùng) trước khi lưu; so thay đổi theo nội dung, không theo tham chiếu.
7. **`HasData`** chỉ dùng hằng số (5.1).
8. **Chống ghi đè cấu hình cần hai lớp**, vì `Revision` của `VerificationChecklist` chỉ là concurrency token (chặn hai lần ghi chồng nhau giữa lúc đọc và lúc lưu), không so với revision client gửi lên:
   - Service so `request.Revision` với giá trị vừa đọc; lệch thì 409.
   - `Revision` là `IsConcurrencyToken()`, và `AppDbContext.SaveChangesAsync` bắt `DbUpdateConcurrencyException` của `PlatformSettings`, ném cùng exception. Tầng Application không bắt được exception của EF; Workspace và VerificationChecklist đang làm đúng cách này (`AppDbContext.cs:55`).
9. **`OnTokenValidated`**: lấy `IUserRepository` từ `context.HttpContext.RequestServices` (scoped), không inject vào chỗ singleton.
10. **Enum trong query** (`role`, `status`): API nhận cả số; `IsInEnum` chặn `1` (Manager cũ).
11. **Thời gian**: mọi `from`/`to` theo UTC; nhóm theo ngày bằng `date_trunc('day', created_at)`.
12. **Thống kê**: `GroupBy` + `Count` trong SQL, không tải bản ghi lên bộ nhớ.
13. **Seed** (A.17): không đặt lại mật khẩu mỗi lần khởi động; không nâng Developer đang có sẵn.

## 10. Ảnh hưởng tới luồng khác và frontend

| Ai | Thay đổi |
|---|---|
| Auth (Bảo) | A.1, A.1b, A.2, A.9 ở đợt 1; seed Admin (A.17) |
| LOCK, workspace (bạn làm, bạn duyệt) | Chữ ký `LockGatePolicy.Evaluate`, `ChallengeRecommendationPolicy.Recommend`; ngân sách đọc từ workspace; chặn Admin trong `WorkspaceAccessGuard` và `WorkspaceService`; `accountLocked` trong danh sách thành viên |
| BUILD (bạn làm, bạn duyệt) | `MaintenanceChangePolicy`; `AiJsonRequest.Purpose`; L9 (nếu đợt 2 chưa làm) |
| VERIFY (Lâm) | Không đổi |
| SHIP (Phúc) | Không đổi |
| Frontend | Màn hình Admin mới; bỏ nút đổi role; tài khoản Admin không có màn hình workspace; xử lý 401 khi tài khoản bị khóa giữa phiên; hiện `accountLocked` cho Manager; không viết cứng 10 điểm/dev, đọc `Budget` từ `GET /lock/check` |

## 11. Kiểm thử

**Unit** (NSubstitute, theo kiểu `DriftThresholdServiceTests`). Tạo mới `AdminServiceTests`, `AdministratorSeederTests`, `PlatformSettingsServiceTests`.

- **A:**
  - GitHub login cả 4 ca với user bị khóa: ném `ACCOUNT_LOCKED`, không gắn GitHub, không gửi email, không lưu `github_connections`.
  - Refresh, verify email với user bị khóa; quên mật khẩu với user bị khóa không tạo mã.
  - Đăng nhập sai mật khẩu với tài khoản bị khóa trả `INVALID_CREDENTIALS` (L11).
  - Khóa user thì `DisconnectUserAsync` được gọi và email được gửi; mở khóa thì không gửi email.
  - `Removed` không mở lại được; đổi trạng thái của tài khoản Admin thì 403; `NO_CHANGE`.
  - Validator phân trang và lọc.
  - Mời user bị khóa hoặc user là Admin thì 404.
  - Admin tạo workspace thì 403; Admin gọi API workspace qua guard thì 404.
  - Seed: đủ 5 trường hợp ở A.17.
- **B:** kiểm giới hạn từng trường; revision cũ thì 409; ghi `SettingsChanged` cho từng trường đổi; workspace mới chép giá trị; **workspace cũ giữ nguyên**; Lock với ngân sách 15/dev; mẫu commit mới được Rule Engine dùng; endpoint thử mẫu.
- **C:** mỗi `outcome` (kể cả `cancelled` và `JsonException`); lỗi ghi nhật ký không làm hỏng lời gọi AI; số đếm của `/ai/usage`.
- **E:** số đếm đúng trên dữ liệu giả.

**HTTP trên DB dev** (unit test không phủ được cấu hình `Program.cs`):
- Admin gọi `/api/admin/*` thì 200 (chưa từng chạy thật); Developer gọi thì 403; không token thì 401.
- Khóa user rồi dùng access token cũ thì 401 (A.2); kết nối hub của user đó bị cắt.
- Khởi động app với `AdminSeed:*` thì đăng nhập được Admin vừa tạo; khởi động lại không đổi mật khẩu.
- `PATCH /users/{id}/role` trả 404; Admin gọi `POST /api/workspaces` thì 403.

Không gọi AI hay GitHub thật khi chưa hỏi (quy ước ở `03-build.md`).

## 12. Quyết định đã chốt và câu hỏi còn mở

### 12.1. Đã chốt

| # | Câu hỏi | Quyết định | Ngày |
|---|---|---|---|
| C2 | Có làm template VERIFY? | Không làm bây giờ; giữ 5 mục cố định (mục 13.1) | 2026-11-21 |
| C4 | Admin chọn model AI trên web? | Không | 2026-11-21 |
| C6 | Tạo tài khoản Admin bằng cách nào? | Seed lúc khởi động từ `AdminSeed:*`. Không nâng Developer lên Admin; xóa API đổi role (A.3, A.17) | 2026-11-21 |
| C9 | Các ý phụ trong ghi chú 2026-11-06 (buộc đăng xuất, Admin ngắt GitHub, gửi lại email xác thực, cảnh báo đăng nhập sai) | Không làm ý nào bây giờ. Buộc đăng xuất giữ thiết kế ở mục 13.5 | 2026-11-21 |
| C12 | Admin có dùng workspace như người thường không? | Không. Chặn ở guard, tạo workspace và mời (A.6, A.16). Ghi BM03 | 2026-11-21 |
| — | Phạm vi Admin | Rút gọn: bỏ D, gọn C và E, bỏ khóa DB, buộc đăng xuất, `lastActiveAt`, email mở khóa, các chốt chặn phụ, tự chuyển Manager (mục 13) | 2026-11-21 |

### 12.2. Còn mở (đề xuất của kế hoạch)

| # | Câu hỏi | Đề xuất | Ai quyết |
|---|---|---|---|
| C1 | Sàn 0.65, khoảng Manager và Dual-Lock: Admin chỉ xem hay được sửa? Có làm strictness level? | Chỉ xem; không làm strictness | Bạn |
| C3 | Manager bị khóa thì workspace không còn ai duyệt: có tự chuyển Manager không? | Bây giờ chỉ làm `accountLocked` (A.14); tự chuyển Manager để sau (mục 13.8), cần nhóm đồng ý | Nhóm (Long, Khoa) |
| C5 | Kiểm trạng thái tài khoản mỗi request hay chỉ ở `/api/admin`? | Mỗi request | Bạn |
| C7 | `Removed` có mở lại được không? | Không, là trạng thái cuối | Bạn |
| C8 | Tài khoản bị khóa vẫn là collaborator trên repo GitHub. Có gỡ tự động? | Không; Manager thấy `accountLocked` và tự xóa thành viên (xóa thành viên đã tự gỡ khỏi repo) | Nhóm |
| C10 | Gửi email khi khóa tài khoản? | Có, bất kể cài đặt thông báo; mở khóa không gửi | Bạn |
| C11 | Email của tài khoản `Removed` bị giữ mãi, người đó không đăng ký lại được | Chấp nhận; muốn đăng ký lại thì phải xóa hoặc ẩn email, chạm vào dữ liệu workspace | Bạn |

## 13. Làm sau nếu còn thời gian

Các phần dưới đây đã thiết kế và đối chiếu với code, nhưng bỏ khỏi phạm vi chính vì tốn công mà giá trị demo thấp. Làm phần nào thì đưa lại vào giai đoạn tương ứng.

### 13.1. Template VERIFY (FR-4.5)

**Vì sao bỏ:** sửa code VERIFY của Lâm (đã xong và có test), có thể phá frontend, chỉ để thay 5 mục đang viết cứng.

**Thiết kế:**
- Bảng `verification_template_items`: `key` (không đổi được, regex `^[a-z][a-z0-9_]{2,49}$`), `label` (≤ 200), `description` (≤ 1000), `position`, `required` (không cho đánh dấu "không áp dụng"), `active`. Seed 5 mục hiện có; `main_user_journey` có `required = true`.
- `verification_checklist_items` thêm `label`, `position`, `required`, chép từ template lúc tạo checklist. Checklist cũ backfill từ 5 mục gốc, join theo `item_key` (migration `AddVerificationChecklists` đã chèn 5 khóa bằng SQL).
- `VerificationChecklist.CreateFor(snapshot, templateItems)` chép các mục `active`.
- `VerificationService`: `RequireKnown` đổi thành "mục có trong checklist này", giữ mã `VERIFY_ITEM_UNKNOWN` (400). Thứ tự dùng `position` thay cho `VerificationItemKeys.All` (`VerificationService.cs:160`, `:177`, `VerificationChecklist.cs:122`). Luật `MainJourneyRequired` thành `required`; mã `VERIFY_MAIN_JOURNEY_REQUIRED` đổi thành `VERIFY_ITEM_REQUIRED` (phá vỡ tương thích) hoặc giữ tên cũ, hỏi Lâm.
- Ràng buộc: luôn còn ít nhất 1 mục `active` và 1 mục `required`; tối đa 20 mục; `key` không trùng; tắt mục thay vì xóa. **Phải chặn template rỗng**: `Recompute` (`VerificationChecklist.cs:128`) đặt `AllItemsChecked = Items.All(...)`, nên danh sách rỗng là `true` ngay và mở khóa SHIP.
- API `GET, POST, PATCH /api/admin/verify-template[/{key}]`; `AdminAction.VerifyTemplateChanged` (`verify_template_changed`).

### 13.2. Chỉ số độ chính xác của drift

**Vì sao bỏ:** có bẫy khi đếm và khó giải thích khi demo.

**Thiết kế:** `overridden / (overridden + đã sửa thật)`. "Đã sửa thật" là alert `resolved` mà submission ở `ResolvedBySubmissionId` có trạng thái khác `Drift` (tức `InScope` hoặc `Approved`). Không dùng thẳng số `resolved`: `BuildAnalysisService.UpdateAlertsAsync` (dòng 256) cũng `Resolve` alert cũ khi một lần submit vẫn drift tạo alert mới thay thế. Submission đã đóng alert không bị đổi trạng thái về sau, vì chỉ submission đang `Analyzing` mới bị chuyển thành `Superseded` (`BuildService.cs:134`).

### 13.3. Phiên bản prompt và p95 của AI

**Vì sao bỏ:** thêm một lớp tính hash và SQL thuần chỉ để có số liệu ít ai xem.

**Thiết kế:**
- Cột `prompt_version` trong `ai_call_logs`: 12 ký tự đầu SHA-256 của system prompt cộng schema. `AiPromptCatalog` tính một lần cho mỗi luồng (các client đã nạp prompt bằng `Lazy<string>`; DEFINE nạp schema qua `AiSchemaBuilder.Build`).
- API `GET /api/admin/ai/prompts`: tên luồng, `prompt_version`, model đang cấu hình; không trả nội dung prompt.
- p95 trong `/ai/usage`: SQL thuần `percentile_cont(0.95) WITHIN GROUP (ORDER BY duration_ms)`, vì EF Core 8 và Npgsql 8 không dịch được hàm phân vị.

### 13.4. Độ trễ API và nhật ký lỗi

**Vì sao bỏ:** middleware chạy trên mọi request, chạm pipeline của cả app; số liệu độ trễ mất khi khởi động lại; thêm một bảng và ghi ở luồng nền của BUILD.

**Thiết kế:**
- `GET /monitoring/endpoints`: middleware đo thời gian mỗi request, gom theo route template vào bộ nhớ (singleton, `ConcurrentDictionary`, giữ 500 mẫu gần nhất mỗi route để tính p95). Đặt **sau** `app.UseExceptionHandler()`; đọc thời gian và mã trạng thái trong `finally`, request có exception thì coi là 500. Route template lấy bằng `context.GetEndpoint() as RouteEndpoint` → `RoutePattern.RawText`; request không khớp route nào gom vào `(unmatched)`. Không tính hub SignalR và request `OPTIONS`.
- Bảng `error_logs`: `method`, `route` (template, không phải đường dẫn thật), `exception_type`, `message` (cắt 1000 ký tự), `trace_id`, `created_at` (có index). Chỉ ghi lỗi 5xx không lường trước, không lưu body request. Ghi từ `GlobalExceptionHandler.cs:38` và 3 chỗ ở luồng nền với `route = "background:build-analysis"`: `BuildAnalysisWorker.cs:49`, `BuildAnalysisWorker.cs:68`, `BuildAnalysisService.cs:75`. Dùng scope riêng và `try/catch`.
- `GET /monitoring/errors?page&pageSize`.
- Số phân tích đang chạy trong overview: thêm thuộc tính chỉ đọc `_running.Count` vào `BuildAnalysisQueue`. Sau đợt 2 (semaphore, O1), con số này vẫn đúng.

### 13.5. Buộc đăng xuất

**Vì sao bỏ:** access token vẫn dùng được tới 30 phút sau đó (tài khoản vẫn `Active` nên A.2 không chặn), nên nút này không làm đúng như tên gọi. Muốn chặn ngay thì khóa tài khoản.

**Thiết kế:** `POST /api/admin/users/{id}/sessions/revoke`: thu hồi mọi refresh token còn sống, gọi `DisconnectUserAsync` (A.2b), ghi `AdminAction.SessionsRevoked` (`sessions_revoked`). Tự thu hồi phiên của mình thì 400 `SELF_ACTION_NOT_ALLOWED`.

### 13.6. Khóa DB, để Admin vẫn khóa được Admin khác

**Vì sao bỏ:** A.4 chọn cách cấm Admin đổi trạng thái của Admin, đơn giản hơn. Cách này chỉ cần nếu muốn cho phép Admin khóa Admin khác; khi đó phải có khóa, vì phép đếm đơn thuần không chặn được hai Admin khóa nhau cùng lúc (đã thử thật). Sẽ là transaction tường minh đầu tiên của codebase.

**Thiết kế:** trước khi đổi trạng thái của một Admin, đếm Admin `Active` khác user đích (`CountActiveAdministratorsExceptAsync`), bằng 0 thì 409 `LAST_ADMINISTRATOR`, và đặt cả phần đếm lẫn lưu trong khóa: `IUserRepository.RunWithAdministratorLockAsync(Func<Task> action)`. Infrastructure: `BeginTransactionAsync` → `SELECT pg_advisory_xact_lock(<hằng số>)` → `action` → `SaveChangesAsync` → `Commit`. **Không ghi `audit_trail` bên trong khóa**: `AppDbContext.SaveChangesAsync` tự lưu lại khi thua cuộc đua nối chuỗi audit, nhưng trong transaction tường minh PostgreSQL hủy cả transaction khi một câu lệnh lỗi, nên lần lưu lại sẽ thất bại.

### 13.7. Lần hoạt động cuối của user

**Vì sao bỏ:** trang `/activity` đã có lịch sử phiên.

**Thiết kế:** `AdminUserListItemDto.lastActiveAt` = `MAX(created_at)` của `refresh_tokens`, lấy bằng subquery trong cùng truy vấn phân trang. Đặt tên "lần hoạt động cuối", vì mỗi lần refresh cũng tạo một dòng.

### 13.8. Tự chuyển Manager khi Manager bị khóa (C3)

**Vì sao bỏ:** nhóm chưa chốt; đổi quyền trong workspace của người khác.

**Thiết kế:** khi khóa một user là Manager của workspace nào, chuyển Manager cho thành viên `Active` vào sớm nhất, ghi `audit_trail` của workspace đó. Nếu làm cùng 13.6 thì phần này phải chạy ngoài khóa.

### 13.9. Chặn Admin ở các chỗ phụ

**Vì sao bỏ:** Admin không bao giờ được mời (A.6), và có lỡ là thành viên thì guard đã chặn (A.16).

**Thiết kế:** `AcceptInvitationAsync` (`invitation.User` là Admin thì 403 `ADMIN_CANNOT_USE_WORKSPACES`); `GetMyWorkspacesAsync` và `GetMyInvitationsAsync` (Admin thì trả rỗng); `WorkspaceHub.JoinWorkspace` (`membership.User` là Admin thì `HubException("WORKSPACE_NOT_FOUND")`).

### 13.10. Email khi mở khóa

**Vì sao bỏ:** C10 chỉ hỏi về email khi khóa.

**Thiết kế:** mở khóa thì gửi email bằng `TrySendAsync`.

## 14. Lịch sử thay đổi của tài liệu

- **2026-11-14:** viết lần đầu, đối chiếu với `feature/verify` (commit `0c6617a`).
- **2026-11-20:** rà trên `main` (commit `cfcbd37`). Bổ sung: gom query vào class để validator chạy (A.5); entity `AdminActionLog` nullable (5.3); phần trùng với đợt 2 (C.1); cách so khớp mẫu commit (B.5); kiểm revision ở service và bắt lỗi concurrency trong `AppDbContext` (mục 9, ý 8); `HasData` chỉ dùng hằng số (5.1); không ghi `audit_trail` trong khóa Admin; bỏ thêm ngân sách vào `WorkspaceDto` vì `GET /lock/check` đã trả (B.3); nơi gọi ngân sách chưa có workspace (B.3); nhật ký AI ghi ở mọi lối ra (C.4).
- **2026-11-21:**
  - Chốt C6 và C12: xóa API đổi role (A.3), nên L5 không còn; seed Admin lúc khởi động (A.17); chặn Admin khỏi workspace ở guard (A.16). Sửa số chỗ gọi guard thành 48; thêm file test của API đổi role vào danh sách cần xóa.
  - Viết lại toàn bộ tài liệu: gom quyết định vào mục 12, thêm bảng phân đợt (7.0), bảng mã lỗi (mục 8), điều kiện "xong khi" cho từng giai đoạn.
  - **Rút gọn phạm vi:** bỏ giai đoạn D (template VERIFY); C chỉ còn nhật ký và số đếm (bỏ phiên bản prompt, p95); E chỉ còn `/monitoring/overview` (bỏ độ trễ API, nhật ký lỗi, chỉ số độ chính xác drift); A bỏ khóa DB, buộc đăng xuất, `lastActiveAt`, email mở khóa, chặn Admin ở chỗ phụ, tự chuyển Manager; gộp controller còn 2 cái mới. Chốt C2, C4, C9. Mọi phần bỏ giữ thiết kế ở mục 13. Kết quả: 2 bảng mới thay vì 4, 9 API thay vì 15, 5 mã lỗi mới thay vì 6, 7 câu hỏi còn mở thay vì 10.
- **2026-11-22:** giai đoạn A đã code trên nhánh `feature/admin` (chưa commit) và chạy kiểm HTTP thật trên DB dev (34 ca đạt). Phát hiện phép đếm "Admin cuối" (A.4) không có tác dụng và hai Admin khóa nhau cùng lúc thì không còn Admin nào. Đã chốt: Admin không đổi trạng thái của Admin (403 `ADMIN_ACCOUNT_PROTECTED`); bỏ `LAST_ADMINISTRATOR`. Thiết kế có khóa DB giữ ở mục 13.6.
