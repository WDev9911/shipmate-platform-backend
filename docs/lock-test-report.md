# Báo cáo unit test: luồng LOCK (kịch bản)

- **Ngày chạy:** 2026-10-19
- **Lệnh:** `dotnet test ShipMate.Tests`
- **Kết quả:** **359 / 359 đạt**, 0 lỗi, 0 bỏ qua.
- **Không gọi AI thật.** AI được giả (`FakeAiGateway`) trả điểm cố định theo tên feature và trả kết quả kiểm tra persona do test đặt sẵn, nên không tốn token.
- **Không gọi GitHub thật.** Việc tạo nhánh được giả bằng NSubstitute.
- **Không dùng DB thật.** Service thật chạy trên repository giả lưu trong bộ nhớ (`LockWorld`), giữ trạng thái giữa các bước nên một test đi được qua nhiều bước như người dùng thật.

## 1. Kết quả theo nhóm test

| Nhóm | Số test | Đạt | Nội dung |
|---|---:|---:|---|
| **Scenarios.SoloDevLockScenarios** | 8 | 8 | Kịch bản dev solo: chấm điểm, kiểm tra, Lock, đóng băng, chấm lại sau khi sửa |
| **Scenarios.TeamLockAndAssignmentScenarios** | 5 | 5 | Kịch bản team 3 người: Lock và nhận feature |
| **Scenarios.FeatureChallengeScenarios** | 14 | 14 | Kịch bản sau Lock: thêm, thay, backup, từ chối, giải phóng người nhận, kiểm tra persona |
| Domain.LockGatePolicyTests | 13 | 13 | Luật 3 lớp kiểm tra |
| Domain.FeatureChallengePolicyTests | 17 | 17 | Gợi ý phương án, tạo snapshot mới, xóa liên đới |
| Domain.MvpSnapshotHasherTests | 4 | 4 | Hash snapshot |
| Domain.FeatureEditContentTests | 4 | 4 | Sửa mô tả/phạm vi thì xóa điểm, chỉ đổi tên thì giữ |
| Application.LockServiceTests | 16 | 16 | Lock, kiểm tra, quyền |
| Application.FeatureChallengeServiceTests | 27 | 27 | Gửi, xác nhận, duyệt, từ chối |
| Application.FeatureAssignmentTests | 25 | 25 | Nhận feature, backup, tạo nhánh GitHub, thử lại tạo nhánh |
| Application.FeatureComplexityTests | 17 | 17 | Chấm điểm AI (AI giả) |
| Application.SkillProfileTests | 15 | 15 | Khai báo skill, khai skill khi nhận lời mời |
| Application.LockedDefinitionTests | 11 | 11 | DEFINE đóng băng sau Lock |
| Nhóm DEFINE (có từ trước) | 117 | 117 | FeatureReview, ProductDefinition, validators, policy... |
| Application.DefineAnalysisOutputValidatorTests | 16 | 16 | Luật hủy output AI sai của DEFINE (xem `unit-test-report.md` mục 10) |
| Application.ProductDefinitionAnalysisServiceTests | 14 | 14 | Phân tích và chạy lại AI của DEFINE, AI giả (mục 11) |
| Application.AuthServiceTests | 36 | 36 | Đăng ký, OTP, đăng nhập, refresh token, đặt lại mật khẩu, GitHub (mục 12) |
| **Tổng** | **359** | **359** | |

## 2. Câu chuyện dùng chung

App "đặt lịch khám" cho phòng khám với 4 feature: **Đăng nhập**; **Bác sĩ tạo lịch khám**; **Xem lịch bác sĩ** (cần lịch do bác sĩ tạo); **Đặt lịch khám** (cần xem lịch). Một số kịch bản thêm **Thanh toán VNPay** (feature đã chốt với khách).

Quy ước điểm: complexity 1 (dễ) đến 5 (khó). Budget = 10 điểm × số dev đang hoạt động. Skill Ceiling = skill cao nhất trong các dev đang hoạt động.

## 3. Kịch bản A: dev solo (Minh, Manager kiêm dev duy nhất, budget 10)

| # | Đầu vào (Given / When) | Kết quả mong đợi (Then) | Kết quả |
|---|---|---|---|
| A1 | 4 feature chưa chấm điểm, skill 4. AI đề xuất 2, 3, 2, 3. Manager xác nhận đúng đề xuất, rồi Lock | Trước khi chấm: báo 4 feature thiếu điểm. Sau khi chấm: tổng 10 = budget 10, qua. Snapshot v1 có 4 feature, 2 dependency, `DevCountAtLock = 1`, hash 64 ký tự. Định nghĩa thành `Locked`, `FirstLockedAt` có giá trị. AI chỉ được gọi 1 lần | Đạt |
| A2 | Điểm 3, 3, 3, 3 (tổng 12), budget 10. Bấm Lock. Sau đó hạ Đăng nhập và Xem lịch xuống 2 | Lần đầu: lỗi `LOCK_GATE_FAILED` (tổng 12 > 10), không có snapshot, vẫn `READY_FOR_LOCK`. Sau khi hạ điểm: tổng 10, Lock thành công | Đạt |
| A3 | Feature đã chốt "Thanh toán VNPay" điểm 5, dev skill 4. Bấm Lock. Sau đó nâng skill lên 5 | Bị chặn, 1 vi phạm Skill Ceiling, gắn cờ `isCommitted = true` (frontend không được gợi ý đẩy sang Kill List). Nâng skill lên 5 thì Lock được | Đạt |
| A4 | Dev chưa khai skill (null), điểm 2, 2, 2, 2. Bấm Lock. Sau đó khai skill 3 | Báo `MembersWithoutSkill = 1`, Lock bị chặn. Khai skill xong thì Lock được | Đạt |
| A5 | Đã Lock. Thử: sửa feature, sửa ràng buộc, chạy AI phân tích, đổi Vision Prompt, chấm điểm lại | Cả 5 đều bị `PRODUCT_DEFINITION_LOCKED`. Tên feature và Vision Prompt không đổi. AI không bị gọi | Đạt |
| A6 | Đã Lock, bấm Lock lần 2 | Bị từ chối, vẫn chỉ 1 snapshot, `FirstLockedAt` không đổi | Đạt |
| A7 | Định nghĩa còn `Draft`, bấm Lock | Lỗi `PRODUCT_DEFINITION_NOT_READY_FOR_LOCK`, không có snapshot | Đạt |
| A8 | 4 feature đã chấm điểm 2. Sửa mô tả "Đặt lịch khám" thành "kèm thanh toán trước", rồi bấm Lock. Sau đó chấm lại 4 | Lần đầu: `LOCK_GATE_FAILED`, đúng 1 feature thiếu điểm là "Đặt lịch khám". Chấm lại xong thì Lock được, snapshot ghi điểm 4 | Đạt |

## 4. Kịch bản B: team 3 người (Minh Manager skill 4, An skill 5, Bình skill 2, budget 30)

Feature: Đăng nhập 2, Bác sĩ tạo lịch 3, Xem lịch 4, Đặt lịch khám 5 (tổng 14).

| # | Đầu vào | Kết quả mong đợi | Kết quả |
|---|---|---|---|
| B1 | Lock team | Qua vì An có skill 5 (feature điểm 5 hợp lệ). `DevCountAtLock = 3`, 4 feature, tổng điểm 14 | Đạt |
| B2 | Xem danh sách nhận feature | Feature điểm 5: chỉ An đủ skill. Feature điểm 2: cả 3 người | Đạt |
| B3 | Bình nhận "Đặt lịch khám" (5). An nhận feature đó. Bình nhận "Đăng nhập" (2) | Bình bị `SKILL_TOO_LOW_FOR_FEATURE` (feature vẫn trống). An được, nhánh `feature/{id}-an`. Bình nhận Đăng nhập được, nhánh `feature/{id}-binh-dev` | Đạt |
| B4 | An nhận "Đăng nhập" trước, rồi Minh cũng nhận | Minh nhận `FEATURE_ALREADY_ASSIGNED`. Danh sách hiện người nhận là An | Đạt |
| B5 | Cường (skill 3) chưa kết nối GitHub nhận feature | Lỗi `GITHUB_NOT_CONNECTED` | Đạt |

## 5. Kịch bản C: Feature Challenge sau Lock (Minh solo skill 5, budget 10)

Snapshot v1: Đăng nhập 1, Bác sĩ tạo lịch 3, Xem lịch 2 (cần tạo lịch), Thanh toán VNPay 1 (đã chốt). Tổng 7.

| # | Đầu vào | Kết quả mong đợi | Kết quả |
|---|---|---|---|
| C1 | Gửi "Nhắc lịch khám", AI đề xuất 3, Manager xác nhận 3, duyệt `AddDirectly` | Gợi ý là `AddDirectly`. Có snapshot v2 (5 feature), feature mới `from_ai` và `locked`. v1 vẫn 4 feature và hash cũ; hash v2 khác v1 | Đạt |
| C2 | Budget đầy (10). Gửi "Báo cáo doanh thu" điểm 2 | Gợi ý `RemoveAndReplace`, bỏ "Xem lịch bác sĩ" (điểm 2: thấp nhất đủ nhường chỗ), **không phải** feature đã chốt | Đạt |
| C3a | Bỏ "Bác sĩ tạo lịch khám" khi "Xem lịch bác sĩ" phụ thuộc vào nó. Lần đầu không chọn cách xử lý, lần sau chọn "tái cấu trúc" | Lần đầu: `DEPENDENTS_RESOLUTION_REQUIRED` liệt kê "Xem lịch bác sĩ", chưa có v2. Sau khi chọn: v2 có feature bị bỏ là `replaced`, "Xem lịch" giữ `locked` và nay phụ thuộc feature mới | Đạt |
| C3b | Như C3a nhưng chọn "xóa luôn feature phụ thuộc" | Cả "Bác sĩ tạo lịch" và "Xem lịch" thành `replaced`, không còn dependency nào | Đạt |
| C4 | Bỏ feature đã chốt "Thanh toán VNPay" không tick xác nhận. Sau đó tick | Không tick: `COMMITTED_REMOVAL_NOT_CONFIRMED`, chưa có v2. Có tick: VNPay thành `replaced` | Đạt |
| C5 | Team có dev cao nhất skill 3. Gửi feature điểm 5. Duyệt `Backup`. Hùng (skill 5) vào và nhận | Gợi ý `Backup`. v2 có feature trạng thái `backup`. Hùng nhận xong thì thành `locked` **trong cùng v2** (không có v3), hash tính lại | Đạt |
| C6 | Manager từ chối kèm lý do "Ngoài phạm vi MVP", rồi thử duyệt lại | Vẫn 1 snapshot. Challenge `Rejected` kèm lý do. Duyệt lại bị `CHALLENGE_ALREADY_DECIDED` | Đạt |
| C7 | An (dev thường) gửi challenge, AI đề xuất 5, An chỉnh thành 2. An thử duyệt, rồi Manager duyệt | An không duyệt được (`WorkspacePermissionDenied`). Manager duyệt được, có v2 | Đạt |
| C8 | Duyệt thêm thẳng feature điểm 5 (tổng thành 12 > 10) | Cổng kiểm tra chặn (`LOCK_GATE_FAILED`, vượt budget). Challenge vẫn `Open`, không có v2 | Đạt |
| C9 | Chưa Lock mà gửi challenge | Lỗi không tìm thấy snapshot | Đạt |
| C10 | Team 3 người (budget 30). An nhận "Bác sĩ tạo lịch", Bình nhận "Xem lịch" (phụ thuộc). Duyệt thay "Bác sĩ tạo lịch", lần đầu không chọn cách xử lý, lần sau chọn xóa liên đới | Lần đầu: `DEPENDENTS_RESOLUTION_REQUIRED` liệt kê "Xem lịch bác sĩ" kèm người nhận là Bình. Sau khi duyệt: cả 2 assignment có `releasedAt` (vẫn còn làm lịch sử), màn hình nhận feature không còn 2 feature đó | Đạt |
| C11 | Budget đầy. Minh nhận "Xem lịch bác sĩ". Gửi "Báo cáo doanh thu" điểm 2 | Gợi ý bỏ "Xem lịch bác sĩ", kèm người đang nhận là Minh | Đạt |
| C12 | Kiểm tra persona trước khi tái cấu trúc sang "Đăng nhập". AI giả báo lệch persona | Trả `personaConflict = true` kèm lý do, lưu vào challenge. Manager vẫn duyệt được, có v2 | Đạt |
| C13 | Kiểm tra persona với feature không có trong snapshot. Sau đó AI trả kết quả sai | Lần đầu: `INVALID_REPLACEMENT_FEATURE`. Lần sau: lỗi AI trả sai. Challenge không lưu kết quả kiểm tra nào | Đạt |

## 6. Các lỗ hổng đã vá và test tương ứng

| Lỗ hổng | Cách vá | Test |
|---|---|---|
| L1 Sửa feature sau khi chấm điểm, Lock dùng điểm cũ | Sửa mô tả hoặc phạm vi thì xóa điểm | A8, `FeatureEditContentTests` |
| L2 Hai lần Lock hoặc duyệt cùng lúc tranh version, lỗi 500 | Bắt lỗi unique, trả `SNAPSHOT_VERSION_CONFLICT` (409) | Chưa test tự động (cần PostgreSQL thật) |
| L3 Bỏ feature đã có người nhận mà Manager không biết | Hiện tên người nhận, giải phóng khi duyệt | C10, C11 |
| L4 Thành viên mới chưa khai skill làm chặn Lock | Bắt buộc khai skill khi nhận lời mời | `SkillProfileTests` |
| N1 Tái cấu trúc sang feature lệch persona | AI kiểm tra, chỉ cảnh báo | C12, C13 |
| N2a Chưa tạo nhánh GitHub thật | Tạo nhánh bằng token của dev nhận, có API thử lại | `FeatureAssignmentTests` |

## 7. Chạy thử trên DB thật (2026-10-19)

Unit test không chạm tới controller, validator, middleware, EF Core và PostgreSQL. Phần này gọi API thật (`http://localhost:5140`) trên DB dev để kiểm tra những lớp đó.

- **Workspace:** tạo mới "LOCK test - Nha Khoa" (`ad75067c-c7da-49ed-a621-3c3e17aaa614`), để workspace demo "Phòng khám Nha Khoa - Đặt lịch" vẫn dùng được cho demo DEFINE (Lock khóa vĩnh viễn DEFINE).
- **Dữ liệu vào:** cùng Vision Prompt và 2 feature đã chốt với bản demo (`define-demo-data.md`), để so được kết quả AI.
- **Tài khoản:** `freelancer.demo` (Manager) và "Dev Test" (Developer, tạo để test luồng team). Mật khẩu ghi trong `define-demo-data.md`, không đưa lên git.
- **AI thật gọi 3 lần:** phân tích DEFINE, đề xuất điểm cho challenge, kiểm tra persona. Điểm feature lúc Lock do Manager chấm tay để tiết kiệm token.
- **GitHub:** chỉ test lỗi `GITHUB_NOT_CONNECTED`, không tạo nhánh thật.
- **Cách đánh giá:** mỗi luật được thử cả hai chiều: làm sai thì phải bị chặn đúng mã lỗi, làm đúng thì phải qua.

### 7.1. DEFINE

| Bước | Kết quả |
|---|---|
| Tạo workspace, thêm 2 feature đã chốt | 201 |
| Chuyển READY trước khi chạy AI | 409, chặn |
| AI phân tích | 200. Persona "Bệnh nhân", vai trò phụ "Bác sĩ", "Chủ phòng khám" (lần 17/10 chỉ có Bác sĩ, AI không ổn định giữa các lần chạy). 7 feature, tất cả `pending_confirmation` |
| EXCLUDE thẳng feature đã chốt / sửa tay feature đã chốt | 409, phải qua change request |
| EXCLUDE feature đang có feature included phụ thuộc | 409, yêu cầu chọn cách xử lý |
| READY khi còn feature pending | 409 |
| INCLUDE 3, EXCLUDE 4, sửa tên feature khách nhắc | 200 |
| READY_FOR_LOCK | 200, `ReadyForLock` |

### 7.2. LOCK

| Bước | Kết quả |
|---|---|
| Kiểm tra khi chưa khai skill, chưa chấm điểm | Báo 1 thành viên thiếu skill, 3 feature thiếu điểm, budget 10. Lock bị chặn (409) |
| Khai skill 6 / skill 4 | 400 / 200 |
| Chấm tay 3, 3, 2 (tổng 8) | 200, vẫn `ReadyForLock` |
| Lock | 201. Snapshot v1: 3 feature `locked`, 1 dependency, `devCountAtLock = 1`, hash 64 ký tự. `firstLockedAt` có giá trị, Product Definition `locked` |
| Lock lần 2, sửa feature, đổi trạng thái, chạy lại AI, sửa Vision Prompt, chấm điểm lại | Tất cả 409 `PRODUCT_DEFINITION_LOCKED`. Chạy lại AI bị chặn trước khi gọi AI. Đọc dữ liệu vẫn 200. `firstLockedAt` không đổi |
| Nhận feature khi chưa kết nối GitHub | 400 `GITHUB_NOT_CONNECTED` |

### 7.3. Feature Challenge

Kịch bản: thay "Quản lý lịch làm việc bác sĩ" (nhập tay) bằng "Đồng bộ lịch bác sĩ từ Google Calendar". Chọn kịch bản này vì nó đi qua nhánh khó nhất: vượt budget, bỏ feature có feature khác phụ thuộc, tái cấu trúc và kiểm tra persona.

| Bước | Kết quả |
|---|---|
| Gửi challenge | 201, AI đề xuất điểm 4 |
| Duyệt khi chưa xác nhận điểm | 409 |
| Xác nhận điểm 4 (8 + 4 = 12 > 10) | Gợi ý `RemoveAndReplace`, bỏ "Quản lý lịch làm việc bác sĩ" (điểm 2, không đụng feature đã chốt), cảnh báo ảnh hưởng tới "Đặt lịch khám online" |
| Duyệt `AddDirectly` khi vượt budget | 409, cổng kiểm tra chạy lại |
| Duyệt thay thế khi chưa chọn cách xử lý feature phụ thuộc | 409 |
| Kiểm tra persona với feature không có trong snapshot / với "Quản lý hồ sơ bệnh án" | 409 / 200, `personaConflict = false` kèm lý do, lưu vào challenge |
| Duyệt `RemoveAndReplace` + `Restructure` | 200. Snapshot v2 (hash khác v1): feature bị bỏ `replaced`, feature mới `from_ai` điểm 4 `locked`, "Đặt lịch khám online" chuyển sang phụ thuộc feature mới. Tổng 10/10 |
| Duyệt lại | 409 `CHALLENGE_ALREADY_DECIDED` |

### 7.4. Luồng team

| Bước | Kết quả |
|---|---|
| Dev chưa là thành viên xem workspace / mời người khác / khai skill khi còn `Invited` | 404. Cố ý: không phải thành viên `Active` thì coi như workspace không tồn tại, để không dò được workspace của người khác |
| Manager mời email không tồn tại / mời dev / mời lại | 404 / 201 (`Developer`, `Invited`) / 409 `WORKSPACE_MEMBER_ALREADY_EXISTS` |
| Dev chấp nhận không gửi skill / skill 9 / skill 2 / chấp nhận lần 2 | 400 / 400 / 200 / 404 |
| Budget sau khi có 2 dev | 20 (`devCountAtLock` của snapshot vẫn là 1, đúng số lúc Lock) |
| Màn hình nhận feature khi dev skill 2, rồi nâng lên 4 | Skill 2: dev không đủ cho feature nào (điểm 3, 3, 4). Skill 4: dev đủ cho cả 3 |
| Dev bấm Lock / từ chối challenge | 403 `WORKSPACE_PERMISSION_DENIED` |

### 7.5. Định dạng lỗi

Sau khi sửa (commit `1482064`), gọi lại các trường hợp lỗi:

| Trường hợp | Kết quả |
|---|---|
| 404 | `NOT_FOUND` |
| FluentValidation (skill 9) | `VALIDATION_FAILED`, `errors.SkillLevel` |
| Body sai kiểu, sai enum, JSON hỏng, field lồng nhau sai kiểu | `VALIDATION_FAILED`, key theo tên field (`SkillLevel`, `Action`, `Items[0].FeatureId`), không lộ chi tiết của bộ đọc JSON |
| Không gửi body | `VALIDATION_FAILED`, `errors.Body` |
| Lỗi nghiệp vụ, gọi thành công | Không đổi |

Server log không có lỗi trong suốt các lần chạy. Unit test vẫn 293/293 sau khi sửa.

### 7.6. Tranh chấp đồng thời trên PostgreSQL thật

Unit test chỉ giả lập chỉ mục unique bằng repository trong bộ nhớ, nên phần này bắn nhiều request **cùng lúc** (curl chạy song song) để kiểm tra chỉ mục thật của PostgreSQL.

Chuẩn bị: gán `github_username` giả (`test-freelancer`, `test-devtest`) trực tiếp trong DB cho 2 tài khoản test, vì nhận feature cần username GitHub. Workspace "LOCK test" chưa liên kết repo nên không có lời gọi nào tới GitHub.

**Hai dev cùng nhận một feature**: mỗi người bắn 3 request cùng lúc, tổng 6 request.

| Feature | Kết quả |
|---|---|
| Quản lý hồ sơ bệnh án | 1 request thắng (Dev Test, 201, nhánh `feature/{id}-test-devtest`), 5 request `FEATURE_ALREADY_ASSIGNED` (409). DB có đúng 1 dòng assignment |
| Đồng bộ lịch bác sĩ từ Google Calendar | 1 request thắng (Manager, 201), 5 request 409. DB có đúng 1 dòng |

Không request nào lỗi 500. Màn hình nhận feature hiện đúng người thắng.

**Sáu lần Lock cùng lúc** trên workspace mới "Race test - Lock" (ghi chú cá nhân, 4 feature, chấm 1 điểm mỗi feature, gọi AI phân tích 1 lần):

| Kết quả | Số request |
|---|---|
| 201, snapshot v1 | 1 |
| 409 `SNAPSHOT_VERSION_CONFLICT`: chạy song song, cùng ghi version 1, bị chỉ mục unique chặn (phần vá L2) | 2 |
| 409 `PRODUCT_DEFINITION_LOCKED`: tới sau khi lần thắng đã lưu | 3 |

DB có đúng 1 snapshot (v1, 4 feature), Product Definition `locked`, `firstLockedAt` bằng giờ tạo snapshot.

### 7.7. Từ chối challenge và bỏ feature đang có người nhận

Trên "LOCK test" (snapshot v2). Lúc bắt đầu: Manager giữ "Đặt lịch khám online" và "Đồng bộ lịch Google", Dev Test giữ "Quản lý hồ sơ bệnh án". Gọi AI thật 2 lần.

| Bước | Kết quả |
|---|---|
| Dev Test gửi "Thông báo lịch hẹn qua Zalo" | 201, AI đề xuất điểm 4. Dev Test chỉnh thành 2 |
| Manager từ chối, lý do "Ngoài phạm vi MVP, để sau khi ra mắt." | 200, `Rejected`, lưu lý do và người quyết định. Snapshot vẫn v2 |
| Duyệt hoặc từ chối lại challenge đó | 409 `CHALLENGE_ALREADY_DECIDED` |
| Manager gửi "Đồng bộ lịch bác sĩ từ Outlook Calendar", xác nhận điểm 4 | 201 (AI đề xuất 4), 200 |
| Bỏ feature đã chốt "Hồ sơ bệnh án" mà không tick xác nhận | 409 `COMMITTED_REMOVAL_NOT_CONFIRMED` |
| Bỏ "Đồng bộ lịch Google" khi chưa chọn cách xử lý feature phụ thuộc | 409 `DEPENDENTS_RESOLUTION_REQUIRED`, `details` liệt kê "Đặt lịch khám online" kèm `assignedTo: Freelance Dev` |
| Duyệt với `Restructure` | 200, snapshot v3: "Đồng bộ lịch Google" `replaced`, "Outlook Calendar" `locked`, "Đặt lịch" chuyển sang phụ thuộc "Outlook Calendar" |
| Assignment trong DB | Chỉ dòng "Đồng bộ lịch Google" có `released_at`. "Đặt lịch" (Manager) và "Hồ sơ" (Dev Test) giữ nguyên |
| Nhận lại feature đã bị thay | 404 `NOT_FOUND` |
| Màn hình nhận feature | "Outlook Calendar" chưa ai nhận; 2 feature còn lại giữ người nhận cũ |

Ghi chú khi chạy: trên Windows, Git Bash truyền body tiếng Việt qua tham số của `curl` bị sai mã hóa. Server trả đúng `VALIDATION_FAILED` và không gọi AI. Script gửi lại body từ file UTF-8 thì chạy bình thường.

## 8. Những gì chưa test

- **Chất lượng AI thật:** AI thật mới chạy 6 lần (mục 7). Chất lượng điểm AI chấm và kết quả kiểm tra persona chưa được đánh giá trên nhiều dữ liệu.
- **GitHub thật:** liên kết repo, tạo nhánh khi nhận feature, đăng nhập và kết nối GitHub qua trình duyệt. Cần người đăng nhập GitHub thật. Logic phía service đã có unit test.
- **Hai lần duyệt challenge cùng lúc:** dùng chung chỉ mục và cách bắt lỗi với Lock (mục 7.6) nhưng chưa bắn song song thật, vì cần 2 challenge (2 lần gọi AI).
- **Chưa làm và nên test sau khi làm:** Branch Protection, SignalR, thông báo feature backup.

**Dữ liệu test còn lại trong DB dev:** workspace "LOCK test - Nha Khoa" (snapshot v3, 3 assignment), "Race test - Lock" (snapshot v1), tài khoản "Dev Test", và `github_username` giả của 2 tài khoản test.
