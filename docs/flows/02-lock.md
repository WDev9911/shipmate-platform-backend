# Luồng 2 (Lock)

Tài liệu này mô tả **phần LOCK đã cài đặt**. Nguyên văn spec LOCK nằm ở tài liệu thiết kế của nhóm; ở đây chỉ ghi cách code hiện hành hiểu và làm, và những chỗ spec để ngỏ.

**Mục đích:** biến bộ feature của DEFINE thành một cam kết không sửa tùy tiện, dựa trên năng lực thực tế của team (solo dev cũng dùng được).

**Phân quyền.** Manager chấm điểm, bấm Lock, duyệt/từ chối Feature Challenge. Mọi thành viên active được xem snapshot, nhận feature và gửi Feature Challenge.

## 1. Skill Profile

- Mỗi `WorkspaceMember` có `SkillLevel` (1–5, `null` nếu chưa khai báo), **gắn theo từng workspace**.
- **Khai khi vào workspace (just-in-time):** chấp nhận lời mời `POST /api/workspaces/{id}/members/accept` **bắt buộc** gửi `{ "skillLevel": 1..5 }`. Thành viên mới vì vậy không bao giờ ở trạng thái chưa khai. **Thay đổi API:** trước đây endpoint này không có body; frontend phải gửi thêm `skillLevel`.
- Sửa lại bất cứ lúc nào: `PUT /api/workspaces/{id}/members/me/skill` với `{ "skillLevel": 1..5 }`. Chỉ thành viên `active`, chỉ sửa skill của chính mình.
- Manager tạo workspace không qua lời mời nên khai bằng API sửa ở trên; Lock bị chặn tới khi Manager khai.
- Giới hạn đã biết của spec: skill là tự khai báo, không có cách xác minh.

## 2. Chấm điểm complexity (bước 1)

- Mỗi feature `included` có hai trường: `ProposedComplexity` (AI đề xuất) và `Complexity` (Manager xác nhận). Cả hai là `null` cho tới khi được chấm.
- `POST /api/workspaces/{id}/product-definition/complexity/propose`: gọi AI chấm toàn bộ feature `included`. AI trả điểm 1–5 kèm lý do; kết quả sai (thiếu feature, điểm ngoài thang, JSON hỏng) thì bị từ chối và không lưu gì.
- `PUT /api/workspaces/{id}/product-definition/complexity` với `{ "items": [{ "featureId", "complexity" }] }`: Manager xác nhận hoặc chỉnh điểm.
- Chấm điểm là input của LOCK nên **không** đưa `READY_FOR_LOCK` về `Draft`.
- **Điểm bị xóa khi nội dung đổi.** Sửa mô tả hoặc phạm vi feature ở DEFINE (sửa trực tiếp, hay qua change request của feature đã chốt) sẽ xóa cả `ProposedComplexity` lẫn `Complexity` của feature đó, để Lock không dùng điểm cũ. Chỉ đổi tên thì vẫn giữ điểm.
- Thang điểm: 1 = một màn hình hoặc endpoint đơn giản; 5 = nhiều yếu tố khó cùng lúc. Prompt ở `Ai/Lock/Prompts/complexity-scoring.system.md`.

## 3. Kiểm tra trước khi Lock (bước 2)

`GET /api/workspaces/{id}/lock/check` chạy kiểm tra mà không Lock, để Manager thấy cái gì còn chặn. Lock chỉ qua khi **tất cả** đều đạt:

| Lớp | Luật |
|---|---|
| Có feature | Phải có ít nhất 1 feature `included` |
| Đã chấm điểm | Mọi feature `included` đã có `Complexity` |
| Đã khai skill | Mọi thành viên `active` đã có `SkillLevel` |
| (a) Skill Ceiling | `Complexity` của từng feature ≤ skill cao nhất trong các thành viên active (feature `backup` được miễn) |
| (b) Budget | Tổng complexity (feature `locked` + `backup`) ≤ 10 × số thành viên active |
| (c) Dependency Integrity | Mọi `depends_on` trỏ tới feature có trong snapshot |

- **Số dev = số thành viên `active`, tính cả Manager** (solo dev: Manager là dev duy nhất, budget = 10).
- Vi phạm (a) được trả kèm cờ `isCommitted` để frontend **không gợi ý đẩy feature đã chốt sang Kill List**; muốn bỏ phải qua xác nhận của Manager.
- Hệ thống không tự xóa liên kết hay feature nào khi gặp vi phạm.

## 4. Lock

`POST /api/workspaces/{id}/lock` (Manager). Điều kiện: Product Definition đã phân tích, đang `READY_FOR_LOCK` (đang `locked` thì lỗi `PRODUCT_DEFINITION_LOCKED`), và qua đủ kiểm tra ở mục 3 (không qua thì lỗi `LOCK_GATE_FAILED`, kèm chi tiết trong `details`).

Khi Lock thành công, trong một lần lưu:
- Tạo **MVP Snapshot version 1** gồm bản sao toàn bộ feature `included` (trạng thái `locked`) và **toàn bộ liên kết phụ thuộc**. Kèm `DevCountAtLock` (đếm đúng 1 lần) và `Hash` SHA-256.
- Product Definition chuyển sang `locked` (DEFINE đóng băng, xem `01-define.md` mục 23).
- `Workspace.FirstLockedAt = now` nếu chưa có; **không bao giờ ghi đè** (SHIP dùng làm mốc Time-to-Ship).

`GET /api/workspaces/{id}/snapshot` trả snapshot mới nhất.

Nếu hai lần Lock (hoặc hai lần duyệt challenge) chạy cùng lúc và tranh nhau một số version, lần sau nhận `SNAPSHOT_VERSION_CONFLICT` (409, "tải lại rồi thử lại") thay vì lỗi 500.

Snapshot không sửa tại chỗ. Ngoại lệ duy nhất theo spec: feature `backup` được nhận thì đổi sang `locked` ngay trong cùng version, và hash được tính lại.

## 5. Feature Assignment

- `GET /api/workspaces/{id}/assignments`: mỗi feature (trừ `replaced`) kèm điểm, trạng thái, người đã nhận (nếu có) và danh sách thành viên **đủ skill** (`SkillLevel ≥ Complexity`).
- `POST /api/workspaces/{id}/assignments/{featureId}`: thành viên tự nhận. Từ chối riêng request đó (feature vẫn trống) khi: chưa khai skill (`SKILL_NOT_DECLARED`), skill thấp hơn complexity (`SKILL_TOO_LOW_FOR_FEATURE`), chưa kết nối GitHub (`GITHUB_NOT_CONNECTED`).
- **Ai bấm trước được trước**, atomic ở tầng DB: chỉ mục unique trên `feature_id`; người đến sau nhận `FEATURE_ALREADY_ASSIGNED`.
- Tên nhánh sinh và lưu ngay: `feature/{feature_id}-{github_username}`.
- **Tạo nhánh thật trên GitHub** nếu workspace đã liên kết repo. Nhánh được tạo từ đầu nhánh mặc định, dùng **token GitHub của chính dev nhận** (người sẽ push). Nhánh đã có sẵn thì coi như thành công.
- GitHub từ chối (hoặc token hết hạn) thì **việc nhận vẫn giữ**, với `branchCreated = false`. Dev thử lại bằng `POST /api/workspaces/{id}/assignments/{featureId}/branch`. Chỉ người đang giữ feature mới gọi được. Chưa liên kết repo thì lỗi `WORKSPACE_REPO_NOT_LINKED`; GitHub vẫn từ chối thì lỗi `GITHUB_BRANCH_CREATION_FAILED`.

## 6. Feature Challenge (sau Lock)

Mọi thay đổi phạm vi sau Lock đi qua đây. Mỗi challenge là một dòng trong `feature_challenges`, đồng thời là audit trail của đề xuất và quyết định.

1. **Gửi:** `POST /api/workspaces/{id}/challenges` với `{ name, description, reason }`. AI đề xuất `ProposedComplexity`. Kết quả AI sai thì không lưu.
2. **Xác nhận điểm:** `PUT .../challenges/{cid}/complexity` với `{ complexity }`. Người gửi (hoặc Manager) xác nhận hoặc chỉnh. Chưa xác nhận thì chưa quyết định được.
3. **Gợi ý (chỉ là gợi ý):** `GET .../challenges/{cid}` trả `recommendation`, tính bằng luật, không gọi AI:
    - Điểm > skill cao nhất của mọi dev → `Backup`.
    - Còn vừa budget → `AddDirectly`.
    - Hết budget → `RemoveAndReplace`, đề xuất 1 feature cần bỏ: **ưu tiên `from_ai`, tránh `from_committed_list`, ưu tiên complexity thấp nhất**, và phải đủ để nhường chỗ. Kèm danh sách feature bị ảnh hưởng (mọi tầng) trong **một** cảnh báo duy nhất.
    - Không feature nào nhường đủ chỗ → `action = null`.
4. **Quyết định (Manager):** `POST .../approve` hoặc `POST .../reject`. Từ chối thì không có gì đổi trong snapshot.

**Duyệt** luôn tạo snapshot version mới (bản sao đầy đủ của bản trước + thay đổi), rồi chạy lại kiểm tra ở mục 3; không qua thì từ chối. Feature mới luôn có `origin = from_ai`.

| Hành động | Hiệu lực |
|---|---|
| `AddDirectly` | Thêm feature mới, trạng thái `locked` |
| `Backup` | Chỉ khi điểm vượt skill của mọi dev (`BACKUP_NOT_APPLICABLE` nếu không). Thêm feature `backup`; khi có dev nhận thì thành `locked` trong cùng version |
| `RemoveAndReplace` | Feature bị bỏ thành `replaced`, feature mới `locked`. Bỏ feature `from_committed_list` phải có `confirmCommittedRemoval = true` |

Với `RemoveAndReplace`, nếu có feature phụ thuộc (trực tiếp hoặc gián tiếp), Manager **bắt buộc** chọn một trong hai (`DEPENDENTS_RESOLUTION_REQUIRED` kèm danh sách nếu thiếu):
- `RemoveDependents`: xóa luôn mọi feature phụ thuộc liên đới (mọi tầng).
- `Restructure`: các feature phụ thuộc trực tiếp chuyển sang phụ thuộc feature thay thế. Mặc định là feature mới; có thể chọn feature khác đang trong snapshot (không được là feature bị bỏ hay feature phụ thuộc vào nó: `INVALID_REPLACEMENT_FEATURE`).

Feature `replaced` vẫn nằm trong snapshot để lưu lịch sử nhưng không tính vào budget, không nhận được, và không còn liên kết nào.

**Người đang giữ feature bị bỏ.** Gợi ý (`featureToRemove`), danh sách `affectedDependents` và lỗi `DEPENDENTS_RESOLUTION_REQUIRED` đều kèm `assignedTo` (tên người đang giữ), để Manager thấy ai mất việc trước khi duyệt. Khi duyệt, mọi feature vừa thành `replaced` được **giải phóng**: assignment có `releasedAt` và người đó không còn đứng tên. Dòng assignment vẫn giữ làm lịch sử.

**Kiểm tra lệch persona khi tái cấu trúc.** Trước khi duyệt `Restructure` sang một feature **có sẵn** (không phải feature mới), Manager gọi `POST .../challenges/{cid}/replacement-check` với `{ replacementFeatureId }`. AI so feature đó với `locked_persona` và trả `{ personaConflict, reason }`. Kết quả này **chỉ là cảnh báo**, không chặn việc duyệt. Lần kiểm tra gần nhất được lưu vào challenge. Feature không có trong snapshot thì lỗi `INVALID_REPLACEMENT_FEATURE`; AI trả sai thì báo lỗi và không lưu gì.

## Quyết định khi triển khai

1. **Số version là số nguyên tăng dần** (1, 2, 3...), không dùng dạng `1.0 → 1.1`. Lock đầu là 1, mỗi Feature Challenge được duyệt tăng 1.
2. **Quy tắc "điểm không được giảm sau khi ghi nhận" chưa cài.** Spec mâu thuẫn với câu "hạ độ phức tạp" khi vượt (a)/(b). Hiện: điểm sửa tự do **trước Lock**; sau Lock không có API sửa điểm của feature đã có trong snapshot, nên điểm không thể giảm. Nếu sau này có API sửa điểm sau Lock thì mới cần thêm luật.
3. **Trạng thái `deferred` chưa có**: spec liệt kê nhưng không định nghĩa. Hiện có `locked`, `backup`, `replaced`.
4. **Gợi ý của Feature Challenge là luật, không phải AI**, để rẻ token, kiểm chứng được và ổn định. AI chỉ dùng để đề xuất complexity.
5. **Branch name lưu cùng bảng `feature_assignments`** thay vì bảng `FEATURE_BRANCHES` riêng (mỗi feature có đúng 1 nhánh).
6. **Complexity của feature chỉ cần cho feature `included`**; feature `excluded` không cần điểm.

## Chưa làm

- **Branch Protection "chỉ ShipMate Bot được ghi"** trên GitHub. Phần này cần một danh tính bot (GitHub App hoặc tài khoản bot) mà hiện chưa có. Nhóm cần quyết định, và nên làm chung với BUILD.
- **Thông báo SignalR** cho dev của feature bị bỏ và của các feature phụ thuộc. Chưa có Hub; dùng chung với BUILD nên chờ thống nhất tên Hub và sự kiện.
- **Thông báo khi có feature `backup` vừa với skill** của dev mới vào hoặc dev vừa sửa skill. Không cần job nền, vì màn hình nhận feature đã liệt kê sẵn feature backup kèm dev đủ skill. Chỉ còn thiếu phần thông báo, làm cùng SignalR.
- **Rủi ro chấp nhận:** nếu dev nhận feature `backup` đúng lúc Manager duyệt một challenge khác, version mới có thể vẫn ghi feature đó là `backup`. Khoảng thời gian rất ngắn nên chưa xử lý.
