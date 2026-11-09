# Báo cáo kiểm thử: luồng BUILD

- **Ngày:** 2026-11-08
- **Unit test:** `dotnet test ShipMate.Tests` cho **772 / 772 đạt**, 0 lỗi, 0 bỏ qua (cả dự án; khoảng 386 test thuộc các nhóm liên quan BUILD, bảng ở mục 2).
- **Chạy thật:** API chạy trên PostgreSQL dev, GitHub App thật (`shipmate-bot-dev`), Gemini thật (`gemini-3.5-flash-lite`). Chi tiết từng lần chạy, mã lỗi và số liệu nằm ở `docs/flows/03-build-chua-lam.md` (mục 3.1 đến 3.9); báo cáo này tóm tắt và chỉ ra điều gì đã chứng minh được, điều gì chưa.
- **Chưa commit:** toàn bộ thay đổi (code, test, tài liệu) còn trong working tree của `feature/build`.

## 1. Cách kiểm thử

Ba tầng, mỗi tầng trả lời một câu hỏi khác nhau:

| Tầng | Trả lời câu hỏi | Công cụ |
|---|---|---|
| Unit test | Logic đúng với mọi nhánh, kể cả ca khó gây ra thật | xUnit, NSubstitute; GitHub, AI, thông báo đều giả |
| Chạy thật qua HTTP | Các phần ghép lại có chạy không; GitHub và Gemini thật hành xử đúng như code giả định không | API thật, tài khoản thử, org `shipmate-test-org`, repo cá nhân thử |
| Đo chất lượng AI | AI chấm phạm vi có đáng tin không | Công cụ ngoài repo gọi đúng `BuildAnalysisClient`, 17 + 5 ca, hạn mức gọi do chủ dự án duyệt |

Tầng 2 có giá trị riêng: nhiều lỗi dưới đây chỉ lộ ra khi gặp GitHub thật.

## 2. Unit test theo nhóm (liên quan BUILD)

| Nhóm | Test | Nội dung |
|---|---:|---|
| BuildAnalysisServiceTests | 46 | Phân tích submission, Rule Engine bỏ qua AI, luật Drift, diff quá lớn, lỗi AI |
| BuildApprovalServiceTests | 38 | Duyệt, fast-forward, Check Run, chặn khi Drift chưa Override |
| BuildServiceTests | 31 | Submit, kiểm tra nhánh, người giữ feature |
| NextBestActionServiceTests | 29 | Gợi ý bước tiếp theo |
| DriftAlertServiceTests | 22 | Ack, Override, trạng thái cảnh báo, `trigger` và `findings` |
| DriftThresholdPolicyTests / ServiceTests | 21 / 18 | Ngưỡng 0.70 đến 0.85, cooldown, giới hạn 3 lần |
| GitHubRepoLinkTests | 21 | Liên kết repo, repo trống, gói miễn phí, đổi repo |
| DirectPushDetectorTests | 18 | `DIRECT_PUSH_DETECTED` (`NO_PULL_REQUEST`, `NO_SCOPE_CHECK`) |
| MaintenanceChangePolicyTests | 17 | Commit chỉ sửa comment thì không gọi AI |
| BuildBranchPolicyTests | 15 | Tên nhánh, `main`, hotfix |
| DriftAlertTests | 13 | Máy trạng thái cảnh báo |
| AuditChainTests / AuditTrailTests | 12 / 5 | Chuỗi hash, phát hiện sửa và xóa |
| WorkspaceSetupServiceTests | 11 | Kiểm tra cài đặt |
| RepoRevocationTests / RepoInvitationTests | 11 / 8 | Mời và gỡ thành viên khỏi repo |
| Còn lại | 61 | Validator, hàng đợi phân tích, `GitHubTokenRefreshTests`, `GitHubAppServiceTests`, `ResendEmailSenderTests`, … |

Các test của R1 nằm ở `BuildAnalysisServiceTests`, `BuildApprovalServiceTests` và `DriftAlertServiceTests` (thêm 11 test).

## 3. Chạy thật, theo phần

| Phần | Nội dung | Kết quả |
|---|---|---|
| 1. HTTP | Setup check, ngưỡng, NBA, hub SignalR | 25/25 đạt |
| 2. GitHub thật | OAuth, liên kết repo, merge gate, thu hồi kết nối | Đạt. Admin của org cũng không merge được khi thiếu `ShipMate Scope Check` |
| 3. Thành viên | Mời, Accept, gỡ khỏi repo | Đạt, GitHub trả 201/204 đúng như code giả định |
| 4. Nhận feature, nhánh bot | Ruleset khóa `feature/**`, chặn xóa/force push, cổng merge | Đạt sau khi sửa lỗi admin bỏ qua khóa nhánh |
| 5. Submit, duyệt | AI thật, fast-forward đúng commit đã phân tích, Check Run, SignalR | Đạt; tìm thấy và sửa lỗ `DIFF_TOO_LARGE` |
| 6. Drift, audit | Drift thật (Stripe 0.0), Ack, Override, ngưỡng, chuỗi audit | Đạt; sửa dữ liệu hoặc xóa bản ghi đều bị phát hiện |
| 7. Ca lỗi | Feature backup, người ngoài workspace, gỡ App, push lén, repo trống, gói miễn phí, 3 lần đổi ngưỡng, tranh chấp đổi ngưỡng | Đạt; tìm và sửa nhiều lỗi (mục 5) |
| 8. Chất lượng AI | Mục 4 | Đạt phần đo; xem giới hạn |

## 4. Chất lượng AI (Phần 8)

**Thiết lập:** 22 ca, 28 lần gọi qua công cụ (hạn mức cứng, chủ dự án duyệt từng đợt), cộng 3 lần chạy thật để kiểm R1. Nhãn kỳ vọng ghi trước khi chạy.

- **`findings` phân biệt tốt:** mọi ca có code ngoài phạm vi đều có `findings` đúng file; ca đúng phạm vi (kể cả diff 42400 ký tự) có 0 `findings`.
- **Điểm số phân biệt kém:** code lệch bị chìm trong code đúng nên điểm 0.75 đến 0.9. Tính theo ca độc lập, ngưỡng mặc định 0.75 để lọt 4 trong 6 ca lệch rõ, và không ngưỡng nào Manager được chọn (0.70 đến 0.85) bắt hết.
- **Đã sửa (R1):** submission là `Drift` khi `điểm < ngưỡng` hoặc AI nêu ít nhất một `findings`. Đo lại: bắt 9/9 lần lệch, 0/9 báo oan ở ca đúng phạm vi. Chạy thật: banner quảng cáo kèm code đúng (điểm 0.80) giờ bị Drift với `trigger: "Findings"`, duyệt thì 409 `DRIFT_UNRESOLVED`.
- **Gợi ý tinh gọn (`suggestions`):** code rối (factory, repository một lần dùng, thư viện thừa) cho 2 gợi ý đúng, code gọn cho 0 gợi ý; diff lớn cho 1 gợi ý hợp lý. Gợi ý không đầy đủ (bỏ sót thư viện thừa), đúng theo prompt "ít mà rõ".
- **Diff lớn:** vượt 60000 ký tự thì `Failed` với `DIFF_TOO_LARGE` và không gọi AI. Không cắt bớt nên code lệch không trốn được ở phần đuôi. Giới hạn: diff cộng dồn so với `main` nên feature quá lớn không phân tích được (chia nhỏ feature ở bước LOCK).

## 5. Lỗi tìm ra và đã sửa

| Lỗi | Lộ ra ở | Cách sửa |
|---|---|---|
| Admin repo vẫn push/xóa được nhánh feature đã khóa | Chạy thật với GitHub | Dùng Rulesets, chỉ App của bot được bypass |
| File diff quá lớn không có `patch` lọt qua giới hạn, AI chấm trên file rỗng | Chạy thật (Phần 5) | Đếm thêm `Changes`; file có thay đổi mà không có patch bị coi là quá lớn |
| Mọi lỗi nghiệp vụ trả `title: "Bad request"` dù mã khác | HTTP | Tiêu đề theo mã HTTP |
| Callback kết nối GitHub 500 vì Resend từ chối email thông báo | Chạy thật | `TrySendAsync` cho email chỉ để thông báo |
| Đăng ký mà Resend từ chối trả 500 chung chung | Phần 7 | 502 `EMAIL_DELIVERY_FAILED`, log lý do |
| Hủy rồi liên kết lại repo làm hở nhánh đã giao | Phần 7 | Liên kết lại khóa lại mọi nhánh đã giao |
| Liên kết vào repo trống làm cổng merge tắt vĩnh viễn | Phần 7 | 409 `GITHUB_REPO_EMPTY` (đã chạy thật ở 3.8) |
| Submit đầu tiên sau khi gỡ App trả 502 thay vì Suspended | Chạy thật | Gặp 401 thì xin token mới và thử lại một lần (đã chạy thật) |
| Repo riêng tư gói miễn phí làm liên kết trả 502 | Chạy thật | Coi 403 "Upgrade to GitHub…" là không bảo vệ được |
| Đổi repo lỗi giữa chừng để repo cũ mất khóa | Chạy thật | Bật cổng ở repo mới trước khi gỡ khóa repo cũ |
| AI cho điểm ≥ ngưỡng dù nêu `findings` (banner quảng cáo lọt) | Phần 6, đo ở Phần 8 | Luật R1 |

## 6. Giới hạn đã biết

- **Cổng AI là lớp phòng thủ theo xác suất.** AI bỏ sót telemetry và đếm lượt xem trang khi chúng nằm trong code đúng (đã gặp 2 lần: một ca đo, một ca chạy thật). R1 chỉ nghe được khi AI nêu `findings`. Quy tắc tĩnh bắt host lạ chưa làm, kèm điều kiện làm ở mục 3.9.
- **Phần bảo đảm cứng không dùng AI:** khóa nhánh `feature/**` bằng Ruleset, bot chỉ fast-forward đúng commit đã phân tích, merge gate (cần repo hỗ trợ), phát hiện push lén, chuỗi audit băm. Không nên nói "mọi thứ ngoài phạm vi đều bị chặn".
- **Mẫu đo nhỏ:** diff tổng hợp, một feature, một ngôn ngữ, một model. Là bằng chứng, chưa phải chứng minh.
- **Ruleset không có trên repo riêng tư gói miễn phí**, chỉ có bản dự phòng không khóa cứng.
- **Gỡ branch protection của `main`** là việc chủ repo làm được; hệ thống chỉ biết khi submit kế tiếp.
- **`BackupFeatureClaimed` giữ nguyên, không sửa (quyết định của chủ LOCK).** Thông báo gửi cho dev giữ feature phụ thuộc vào feature backup; qua API hiện không feature nào phụ thuộc feature backup nên chưa ai nhận được, chỉ có unit test. Không ảnh hưởng luồng. Cách xử lý feature backup vẫn là: để nguyên ở `backup` và mời dev đủ skill (`BackupFeatureMatched`).
- Chưa có endpoint HTTP kiểm chuỗi audit; chuỗi được kiểm bằng công cụ đọc DB.
- Hai nơi định nghĩa "đã có GitHub" khác nhau (setup check và Accept); chưa tra theo `github_id`; archive workspace không gỡ thành viên khỏi repo.
- Chưa test: frontend và CLI (chưa có), `HasOpenDriftAsync` (chờ VERIFY), merge PR thật vào `main` của repo thử (cố ý không làm), diff gần 60000 ký tự toàn logic, diff lớn có đoạn lệch nhỏ chôn giữa.
- Hạn mức gọi AI là điều kiện của phép đo: kết quả dựa trên số lần gọi ít, không phải thống kê.

## 7. Dữ liệu còn lại để bạn dọn tay

- Repo `24HBG/shipmate-personal-test` (công khai, `main` còn các commit thử) và `24HBG/shipmate-empty-test` (riêng tư, chỉ có README).
- Lời mời cộng tác viên repo của `hbob8686` trên `demo-app` có thể còn treo.
- Khóa Resend và Gemini: nội dung `appsettings.Development.json` từng hiện trong một cuộc chat, nên đổi nếu lo ngại.
