# BUILD: việc chưa làm và chưa kiểm chứng

Ghi lúc kết thúc GĐ 0–8 (2026-10-27) để mở chat mới vẫn biết còn gì. Chi tiết từng giai đoạn nằm trong [03-build.md](03-build.md).

Tình trạng: nhánh `feature/build`, commit cuối `4143d61`. Cả 9 giai đoạn đã viết code, 686/686 unit test đạt, migration đã áp vào DB dev. **Chưa commit gì.**

## 1. Việc của bạn (không phải code)

- [x] **Dừng backend** đang chạy trong tab terminal "ShipMate API" (`Ctrl + C`).
- [x] **Commit và push BUILD.** Chọn từng file, không dùng `git add .`. Không đẩy: `.claude/`, `define-demo-data.md`, `github-login-scenario.sh`, `login-scenario.sh` (có mật khẩu thử nghiệm). Khoảng 26 file sửa và 40 mục mới chưa theo dõi.
- [x] **PR #14 (DEFINE) và #15 (LOCK)** đã merge vào `main` (2026-10-23). PR BUILD từ `feature/build` sẽ chỉ chứa phần BUILD.
- [ ] **Đổi khóa Resend và Gemini** nếu lo ngại: nội dung `appsettings.Development.json` đã hiện trong một cuộc chat.

## 2. Việc đã hoãn, cần hỏi lại bạn

- [x] **Endpoint xem lịch sử đổi ngưỡng** (drift threshold): đã làm 2026-10-29, `GET /api/workspaces/{id}/drift-threshold`. Chưa commit. Mô tả ở cuối mục GĐ 6 của `03-build.md`.
- [ ] **Bỏ `UserRole.Manager`?** Giá trị này không dùng (Manager chỉ là vai trò theo từng workspace, `WorkspaceMemberRole`). Chưa quyết, không xóa khi chưa hỏi.
- [ ] **`github_username` giả** trong DB dev (`test-freelancer`, `test-devtest`): chưa trả lời có đổi lại không.
- [ ] **Màn hình "setup check"** (kiểm tra cài đặt App/repo): mình đã đề xuất, bạn chưa duyệt.
- [ ] **Tự mời người vào repo GitHub**: đã đề xuất, chưa duyệt.

## 3. Chưa kiểm chứng (cần bạn cho phép dùng GitHub/AI thật)

Chỉ mới kiểm chứng bằng mock, PostgreSQL thật (tranh chấp AuditTrail, tranh chấp đổi ngưỡng) và Hub SignalR bằng máy chủ tối thiểu.

- [ ] **GitHub thật:** lấy token cài đặt, branch protection và merge gate (`ShipMate Scope Check`), Compare API, fast-forward nhánh, Check Run, tra PR/check-run theo commit.
- [ ] **AI thật:** chất lượng prompt, phân bố điểm confidence (có hợp với ngưỡng 0.75 không).
- [ ] **Chạy qua HTTP:** các endpoint, worker phân tích nền, nối SignalR trong `Program.cs`.
- [ ] **Truy vấn PostgreSQL** `GetOpenByWorkspaceAsync` và `HasOpenDriftAsync`.
- [ ] **Frontend và CLI:** chưa có gì để thử.

Gợi ý chạy thử: cài App lên `shipmate-test-org/demo-app`, chạy luồng submit rồi approve bằng `curl`, gọi AI thật vài lần để xem điểm. Cần bạn đồng ý trước.

## 4. Giới hạn đã biết (đã ghi ở mục 10 của 03-build.md)

- Repo cá nhân không khóa cứng được (không có branch protection).
- Workspace liên kết repo trước GĐ 5 chưa có merge gate, phải liên kết lại.
- Quét `main` tối đa 300 commit.
- Người bị xóa khỏi workspace vẫn nghe sự kiện đến khi ngắt kết nối.
- Sự kiện `BranchPushSuccess` chưa kèm danh sách commit.
- Hàng đợi phân tích, khóa và SignalR chỉ chạy đúng với một instance.

## 5. Cần trao đổi với người khác

- [ ] **Phúc (SHIP) và Lâm (VERIFY):** theo mục 12 của `03-build.md`.
- [ ] **Thầy Vũ:** nâng ngưỡng có tính vào giới hạn 1 lần/7 ngày và 3 lần/dự án không? (Hiện đang tính, phương án A.)
- [ ] **Frontend:** báo các API, mã lỗi và sự kiện SignalR mới của BUILD.

## 6. Quyết định đã chốt (để không hỏi lại)

- Ngưỡng drift mặc định 0.75, khoảng 0.70–0.85, sàn cứng 0.65, 1 lần đổi/7 ngày, 3 lần/dự án; nâng ngưỡng cũng tính một lần đổi.
- Luật vào `main` áp dụng cả với admin GitHub (`enforce_admins = true`).
- Manager là vai trò theo workspace, không phải vai trò tài khoản.

## 7. Quy tắc làm việc của bạn

- Việc ngoài kế hoạch phải hỏi trước; viết code theo kiểu YAGNI.
- Bạn tự chạy git commit/push; chỉ chạy hoặc dừng app khi bạn bảo.
- Không dùng GitHub/AI thật khi chưa được duyệt.
- Khóa `.pem` nằm ngoài repo, không đọc và không in nội dung.
