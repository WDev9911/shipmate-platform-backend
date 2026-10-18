# Báo cáo unit test: luồng LOCK (kịch bản)

- **Ngày chạy:** 2026-10-19
- **Lệnh:** `dotnet test ShipMate.Tests`
- **Kết quả:** **293 / 293 đạt**, 0 lỗi, 0 bỏ qua, chạy trong khoảng 0,13 giây.
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
| **Tổng** | **293** | **293** | |

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

## 7. Những gì chưa test

- **Không gọi AI thật.** Chất lượng điểm AI chấm, kết quả kiểm tra persona và phần prompt chưa được kiểm chứng. Chỉ test cách hệ thống xử lý kết quả AI (hợp lệ, sai thang, thiếu feature, JSON hỏng).
- **Không có DB thật.** Chỉ mục unique chống hai người nhận cùng feature (FCFS) và chống tranh version snapshot được giả lập; hành vi thật của PostgreSQL chưa được kiểm tra tự động.
- **Không gọi GitHub thật.** Chưa thử tạo nhánh trên một repo thật.
- **Chưa test HTTP/controller.** Hai migration đã chạy vào DB dev.
- **Chưa làm và nên test sau khi làm:** Branch Protection, SignalR, thông báo feature backup.
