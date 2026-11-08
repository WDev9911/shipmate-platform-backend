# BUILD: việc chưa làm và chưa kiểm chứng

Ghi lúc kết thúc GĐ 0–8 (2026-10-27) để mở chat mới vẫn biết còn gì. Chi tiết từng giai đoạn nằm trong [03-build.md](03-build.md).

Tình trạng: nhánh `feature/build`, commit cuối `4143d61`. Cả 9 giai đoạn đã viết code, 686/686 unit test đạt, migration đã áp vào DB dev. **Chưa commit gì.**

Cập nhật 2026-11-07: nhánh đã có commit BUILD (`887fd28`, `5ffd60a`); setup check và tự mời vào repo vẫn chưa commit. 708/708 unit test đạt. Phần 1 của kế hoạch test (chạy qua HTTP, không cần GitHub/AI) đã xong, xem mục 3.1.

## 1. Việc của bạn (không phải code)

- [x] **Dừng backend** đang chạy trong tab terminal "ShipMate API" (`Ctrl + C`).
- [x] **Commit và push BUILD.** Chọn từng file, không dùng `git add .`. Không đẩy: `.claude/`, `define-demo-data.md`, `github-login-scenario.sh`, `login-scenario.sh` (có mật khẩu thử nghiệm). Khoảng 26 file sửa và 40 mục mới chưa theo dõi.
- [x] **PR #14 (DEFINE) và #15 (LOCK)** đã merge vào `main` (2026-10-23). PR BUILD từ `feature/build` sẽ chỉ chứa phần BUILD.
- [ ] **Đổi khóa Resend và Gemini** nếu lo ngại: nội dung `appsettings.Development.json` đã hiện trong một cuộc chat.

## 2. Việc đã hoãn, cần hỏi lại bạn

- [x] **Endpoint xem lịch sử đổi ngưỡng** (drift threshold): đã làm 2026-10-29, `GET /api/workspaces/{id}/drift-threshold`. Chưa commit. Mô tả ở cuối mục GĐ 6 của `03-build.md`.
- [x] **Bỏ `UserRole.Manager`:** đã bỏ 2026-11-07, chưa commit. Manager chỉ còn là vai trò trong workspace (`WorkspaceMemberRole`), người tạo workspace tự thành Manager. `UserRole` còn `Developer = 0`, `Administrator = 2`; giữ nguyên số vì API nhận cả số cho enum, nên `1` (Manager cũ) bị từ chối thay vì bị hiểu thành Administrator. DB dev không có tài khoản nào mang role `manager`, không cần migration. Thêm 3 unit test (`UpdateUserRoleRequestValidatorTests`), 711/711 đạt.
- [x] **`github_username` giả** trong DB dev: đã đổi sang tài khoản thật 2026-11-04 (`test-devtest` → `baohgse182460-bot`, `test-freelancer` → `hbob8686`). Chỉ sửa DB dev, chưa kiểm chứng bằng GitHub thật.
- [x] **Màn hình "setup check"** (kiểm tra cài đặt App/repo): đã làm phần backend 2026-10-29, `GET /api/workspaces/{id}/setup` (chỉ Manager). Đã chạy qua HTTP 2026-11-06 (mục 3.1), chưa thử với repo đã liên kết thật. Chưa commit.
- [x] **Tự mời người vào repo GitHub**: đã làm và thử thật 2026-11-04, chưa commit. Khi thành viên chấp nhận lời mời vào workspace, backend gọi `IGitHubAppService.InviteCollaboratorAsync` (`PUT /repos/{owner}/{repo}/collaborators/{username}`, quyền `push`). Lỗi GitHub không làm hỏng việc tham gia, người được mời phải cấp quyền tay. Thử thật: App `shipmate-bot-dev` cài lên `shipmate-test-org` (chỉ `demo-app`), GitHub trả 201 và `hbob8686` hiện "Pending Invite, Role: write". Lời mời chưa được chấp nhận, lời mời thử còn nằm ở `demo-app` cho đến khi bị xóa tay. Phần liên kết repo đã chạy thật ngày 2026-11-07 (mục 3.2); lúc thử ngày 2026-11-04 thì chưa, nên workspace thử được điền repo thẳng vào DB. Sau đó đã làm thêm (2026-11-07, mục 3.3): mời khi liên kết repo, thu hồi khi xóa thành viên, bỏ liên kết hoặc đổi repo.

## 3. Chưa kiểm chứng (cần bạn cho phép dùng GitHub/AI thật)

Chỉ mới kiểm chứng bằng mock, PostgreSQL thật (tranh chấp AuditTrail, tranh chấp đổi ngưỡng) và Hub SignalR bằng máy chủ tối thiểu.

- [x] **GitHub thật:** lấy token cài đặt, liên kết repo, branch protection, merge gate (mục 3.2), nhánh do bot tạo và khóa (mục 3.4), Compare API, fast-forward, Check Run (mục 3.5), tra PR và check-run theo commit để phát hiện push lén (`NO_PULL_REQUEST`, `NO_SCOPE_CHECK`), App bị gỡ, repo trống, repo cá nhân và gói miễn phí (mục 3.7, 3.8). Giới hạn: ruleset không có trên repo riêng tư gói miễn phí (chỉ chạy được bản dự phòng, đã ghi ở các mục đó).
- [x] **AI thật:** mục 3.5 (đúng phạm vi 1.0), Feature Challenge với AI (mục 3.7), phân bố điểm và chất lượng phân tích trên 28 lần gọi (Phần 8, mục 3.9: `findings`, gợi ý tinh gọn, diff lớn, luật R1 chạy thật). Giới hạn đã biết: AI có thể bỏ sót telemetry và đếm lượt xem (mục 3.9); mẫu đo nhỏ, một feature, một model.
- [x] **Chạy qua HTTP:** Phần 1 (mục 3.1), submit/duyệt/SignalR (mục 3.5), drift, Acknowledge, Override, ngưỡng và chuỗi audit (mục 3.6), các ca lỗi của Phần 7 (mục 3.7, 3.8). Không có endpoint HTTP nào kiểm chuỗi audit, nên chuỗi được kiểm bằng công cụ đọc DB.
- [ ] **Truy vấn PostgreSQL:** `GetOpenByWorkspaceAsync` đã chạy trên DB thật nhưng chưa có drift alert nào, nên mới thấy kết quả rỗng. `HasOpenDriftAsync` chưa có endpoint nào gọi (để dành cho VERIFY), chưa chạy được.
- [ ] **Frontend và CLI:** chưa có gì để thử.

### 3.1. Đã chạy qua HTTP (Phần 1 của kế hoạch test, 2026-11-06 và 2026-11-07)

API chạy thật trên DB dev, tài khoản thử trong `define-demo-data.md`. Không dùng GitHub hay AI thật. 25/25 kiểm tra đạt, cộng phần đổi ngưỡng tới giới hạn bên dưới.

- [x] **Setup check** `GET /setup` (workspace "LOCK test - Nha Khoa"): Manager 200, Dev 403, không token 401, workspace không tồn tại 404 `NOT_FOUND`. Trả `ready: false`, `repoLinked: false`. Cả hai tài khoản nằm trong `membersWithoutGitHub` vì `github_username` được sửa thẳng trong DB, chưa có kết nối OAuth.
- [x] **Đọc ngưỡng** `GET /drift-threshold`: Dev đọc được, không token 401.
- [x] **Đổi ngưỡng** `PUT /drift-threshold`, chạy trên workspace thử riêng "BUILD HTTP test - nguong" (`e6cc7b2f-5382-4f01-b7b6-8c38560f0d08`, đã dùng hết 3 lần đổi):
  - 0.9 và 0.6 bị 400 (`THRESHOLD_OUT_OF_RANGE`), giữ nguyên 0.75 bị `THRESHOLD_UNCHANGED`, gửi chữ bị `VALIDATION_FAILED`, Dev đổi bị 403.
  - Đổi 0.75 → 0.70 thành công; đổi tiếp ngay bị 409 `THRESHOLD_CHANGE_COOLDOWN`.
  - **Giới hạn 3 lần:** tạm giảm `Cooldown` xuống 5 giây (đã trả về 7 ngày, 708/708 unit test đạt), đổi 0.70 → 0.80 rồi 0.80 → 0.72, lần thứ 4 bị 409 `THRESHOLD_CHANGE_LIMIT_REACHED`. Lịch sử trả đủ 3 lần.
  - **Bảng `audit_trail`:** 5 bản ghi `THRESHOLD_CHANGED`, `GOVERNANCE_DILUTION`, `THRESHOLD_CHANGED`, `THRESHOLD_CHANGED`, `GOVERNANCE_DILUTION`. `GOVERNANCE_DILUTION` chỉ có khi hạ ngưỡng, nâng ngưỡng không có. Chuỗi hash liền mạch (kiểm bằng `AuditChain.FindFirstBroken`).
- [x] **NBA** `GET /next-best-action`: Dev nhận `WorkOnFeature` "Quản lý hồ sơ bệnh án", Manager nhận `WorkOnFeature` "Đặt lịch khám online", workspace chưa Lock nhận `NoAction`. `GET /build/alerts` trả danh sách rỗng.
- [x] **Hub SignalR** `/hubs/workspace` trong app thật (client Node tối giản): negotiate không token 401, có token trong query thì 200; `JoinWorkspace` vào workspace của mình được, vào workspace khác bị `WORKSPACE_NOT_FOUND`; `LeaveWorkspace` được. Chưa nhận sự kiện nào, vì chưa có hành động nào phát sự kiện (snapshot không có feature `backup`).

**Điểm phát hiện, đã sửa (2026-11-07):** mọi lỗi nghiệp vụ từng trả `"title": "Bad request"` dù mã HTTP là 409 hay 403. Giờ tiêu đề theo mã HTTP (409 "Conflict", 403 "Forbidden", 502 "Bad Gateway"; 400 giữ "Bad request"). Lỗi nghiệp vụ dự kiến cũng chỉ ghi log mức Information, không in stack trace.

### 3.2. Đã chạy với GitHub thật (Phần 2 của kế hoạch test, 2026-11-07)

API chạy bằng profile `https` (`dotnet run --project ShipMate.API --launch-profile https`, cổng 7138), vì `GitHub:RedirectUri` là `https://localhost:7138/api/auth/github/callback` và cookie `state` có cờ `Secure`. Máy cần tin chứng chỉ dev một lần (`dotnet dev-certs https --trust`). Workspace "LOCK test - Nha Khoa", Manager kết nối với GitHub `24HBG`, repo `shipmate-test-org/demo-app`.

- [x] **Kết nối GitHub (OAuth):** `GET /auth/github/connect-url` trả link đúng (`client_id`, `redirect_uri`, scope `read:user user:email repo`, `state`). Bấm Authorize thật xong, `github_username` thành `24HBG` và Manager hết nằm trong `membersWithoutGitHub`. `state` sai thì chuyển về `?error=invalid_state`.
- [x] **Liên kết repo:** `GET /github/repos` trả 31 repo, có `demo-app`. Repo không có quyền (`torvalds/linux`) bị 403 `GITHUB_REPO_NOT_ACCESSIBLE`; Dev gọi liên kết bị 403. Liên kết `demo-app` trả 200 với `gitHubRepoIsOrganization: true`, `gitHubMergeGateEnabled: true`; liên kết lần hai vẫn 200, không lỗi.
- [x] **Merge gate thật trên GitHub:** nhánh `main` có quy tắc bắt buộc check `ShipMate Scope Check` (do `shipmate-bot-dev` phát hành) và bật "Do not allow bypassing the above settings" (áp dụng cả admin). Sửa `README.md` trên web bằng `24HBG` (Owner của org) bị chặn: "You can't commit to main because it is a protected branch".
- [x] **Setup check:** sau khi liên kết `repoLinked`, `appInstalled`, `mergeGateEnabled`, `repoIsOrganization` đều `true`; `ready` còn `false` vì Dev chưa kết nối GitHub. Bỏ liên kết (`DELETE /github`, 204) thì các cờ về `false`; liên kết lại thì trở lại `true`.
- **Điều kiện đã gặp:** `GET /github/repos` không thấy repo của `shipmate-test-org` cho tới khi org cấp quyền cho OAuth App `ShipMate` (mục Organization access trong `https://github.com/settings/connections/applications/<ClientId>`). Với org mới, Manager phải làm bước này trước khi liên kết.
- [x] **Liên kết repo chưa cài App:** `24HBG/NutriCook` bị 409 `GITHUB_APP_NOT_INSTALLED` kèm `installUrl`; lỗi không làm mất liên kết `demo-app` đang có.
- [x] **Bỏ liên kết không gỡ bảo vệ nhánh:** sau `DELETE /github` (204), trang Settings → Branches của `demo-app` vẫn còn quy tắc `main`. Đúng như code (chỉ xóa dữ liệu trong DB). Hệ quả: người dùng bỏ liên kết xong, repo vẫn bị khóa `main`.
- [x] **Thu hồi kết nối OAuth** `DELETE /auth/github/connection`:
  - Còn workspace đang liên kết repo thì bị 409 `GITHUB_DISCONNECT_BLOCKED_BY_LINKED_WORKSPACES`, kết nối giữ nguyên.
  - Bỏ liên kết trước thì thu hồi được (204): `github_username` về `null`, Manager quay lại `membersWithoutGitHub`, `GET /github/repos` và `POST /github/link` trả 400 `GITHUB_NOT_CONNECTED`.
  - Kết nối lại: GitHub tự chấp nhận vì đã cấp quyền từ trước (không hiện màn hình Authorize), kết nối lưu thành công, liên kết lại `demo-app` được (200).
- **Lỗi phát hiện, đã sửa (2026-11-07):** callback kết nối GitHub từng trả **HTTP 500** thay vì chuyển hướng về `/settings?github=connected`. Nguyên nhân: `AuthService.ConnectGitHubAsync` gửi email "Your GitHub account has been linked" qua Resend sau khi đã lưu kết nối; Resend từ chối địa chỉ `freelancer.demo@example.com` (422), ngoại lệ không được bắt nên cả request lỗi dù kết nối đã lưu. Cách sửa: thêm `IEmailSender.TrySendAsync` (lỗi thì ghi log cảnh báo và trả `false`, không ném), dùng cho 3 email chỉ để thông báo (GitHub đã kết nối, báo liên kết khi đăng nhập GitHub, mời vào workspace). Email mã xác minh và đặt lại mật khẩu vẫn dùng `SendAsync` nên vẫn báo lỗi khi Resend hỏng. Kiểm tra: thêm 4 test cho `ResendEmailSender` (tổng 715/715); kết nối lại GitHub thật, callback chuyển về `/settings?github=connected` và log ghi "Notice email ... was not sent" (422).

Gợi ý chạy thử: cài App lên `shipmate-test-org/demo-app`, chạy luồng submit rồi approve bằng `curl`, gọi AI thật vài lần để xem điểm. Cần bạn đồng ý trước.

### 3.3. Mời thành viên và mời vào repo GitHub (Phần 3 của kế hoạch test, 2026-11-07)

Workspace "LOCK test - Nha Khoa" đã liên kết `shipmate-test-org/demo-app`; Dev Test (`se182460hoanggiabao@gmail.com`) và tài khoản GitHub thật `hbob8686`.

- [x] **Mời vào workspace (HTTP, 9/9 đạt):** email không tồn tại 404; Dev mời người khác 403; mời người đang là thành viên hoặc đang `Invited` 409; Dev xem `GET /workspaces/invitations` thấy lời mời; Dev từ chối 204 rồi mời lại 201; xóa thành viên 204 rồi mời lại 201. Mời lại luôn dùng lại đúng dòng cũ, không tạo dòng trùng.
- [x] **Accept khi đã là collaborator (B3):** GitHub trả 204 (đã có quyền), Dev Test thành `Active`, không lỗi, không tạo lời mời mới.
- [x] **Accept mời mới thật (B1, B2):** sau khi gỡ `hbob8686` khỏi Collaborators, Dev Test rời workspace, mời lại, Accept: backend gọi `PUT /repos/shipmate-test-org/demo-app/collaborators/hbob8686` và GitHub trả 201. `hbob8686` chấp nhận ở `/invitations` và có quyền push (banner "You now have push access").
- [x] **Gán tên GitHub:** DB dev đổi `github_username` của Dev Test từ `baohgse182460-bot` sang `hbob8686` (để thử). `github_id` vẫn `null` vì chưa kết nối OAuth, nên setup check vẫn liệt kê Dev Test trong `membersWithoutGitHub`. Kết nối OAuth bằng cửa sổ đang đăng nhập `24HBG` bị chặn `GITHUB_ACCOUNT_ALREADY_LINKED` (đúng thiết kế, một tài khoản GitHub chỉ gắn một user).
- [x] **Thông báo email mời:** email mời gửi đi bằng `TrySendAsync`; lỗi gửi chỉ ghi log, không làm hỏng lời mời.

**Quy tắc: workspace là nguồn quyền vào repo (đã làm và chạy thật 2026-11-07).** Thành viên `Active` thì có quyền vào repo; không còn `Active` thì mất quyền.

- **Accept bắt buộc đã kết nối GitHub:** chưa có `github_username` thì 400 `GITHUB_REQUIRED_TO_JOIN`, lời mời vẫn `Invited`. Người đăng nhập bằng GitHub luôn thỏa; người đăng ký bằng email phải kết nối ở Settings trước. Phản hồi Accept có thêm `repoInvite`: `sent`, `alreadyHasAccess`, `failed` hoặc `noRepo`. Mời lỗi thì ghi audit `REPO_INVITE_FAILED`, việc vào workspace vẫn thành công. **Thay đổi API: báo frontend.**
- **Liên kết repo mời mọi thành viên `Active`** có tên GitHub (bỏ qua Manager). Vá lỗ hổng "liên kết repo sau khi đã có thành viên". Thử thật: Dev Accept lúc chưa có repo (`repoInvite: noRepo`), liên kết repo, GitHub có lời mời treo cho `hbob8686`.
- **Xóa thành viên gỡ khỏi repo trước, rồi mới đặt `Removed`.** GitHub lỗi tạm thời thì 502 `GITHUB_REVOKE_FAILED` và thành viên vẫn `Active`, Manager thử lại. 404/410 hoặc App đã bị gỡ cài đặt thì coi là xong. `DELETE …/members/{userId}?force=true` xóa dù GitHub lỗi và ghi audit `REPO_REVOKE_SKIPPED`. Thử thật: collaborator bị gỡ, lời mời đang treo bị hủy, người không còn là collaborator vẫn 204.
- **Bỏ liên kết repo hoặc đổi sang repo khác** gỡ mọi thành viên (trừ Manager) khỏi repo cũ trước; lỗi thì giữ nguyên liên kết. Thử thật: bỏ liên kết thì `hbob8686` biến khỏi Collaborators.
- **Ngắt kết nối GitHub** bị chặn (`GITHUB_DISCONNECT_BLOCKED_BY_LINKED_WORKSPACES`) khi người đó là thành viên `Active` của workspace đang liên kết repo (trước đây chỉ chặn Manager). Thử thật: Dev bị 409.
- **Hành vi GitHub đã kiểm chứng:** `DELETE …/collaborators/{u}` trả 204 dù người đó không phải collaborator (gỡ lặp lại an toàn) và **không** hủy lời mời đang treo, nên backend hủy thêm bằng `DELETE …/invitations/{id}`.

**Giá phải chấp nhận:**
- Người dùng chưa có GitHub không vào được workspace; frontend phải điều hướng họ qua bước kết nối.
- GitHub sự cố ngắn thì Manager tạm không xóa, bỏ liên kết hoặc đổi repo được (có `?force=true` cho việc xóa thành viên).
- **Giả định: workspace ShipMate là nguồn quyền duy nhất của repo.** Người bị xóa khỏi workspace mất quyền dù được Manager thêm tay; người ngoài workspace có quyền cấp tay thì ShipMate không thấy và không quản.
- Dựa vào `github_username` lưu trong DB: người đổi tên GitHub (tên cũ bị người khác chiếm) có thể bị mời hoặc gỡ nhầm. Chưa tra theo `github_id`.
- Archive workspace không gỡ ai (chỉ đổi trạng thái, giữ thành viên và liên kết repo).
- Kẽ hở còn lại: Dev Accept lúc chưa có repo, ngắt kết nối GitHub trước khi repo được liên kết (được phép vì chưa có repo), rồi repo được liên kết: người đó `Active` nhưng không có tên GitHub nên không được mời.
- Quy tắc "sau Lock không thêm người" **không có** trong code (Lock chỉ khóa phạm vi sản phẩm, không khóa danh sách thành viên); cần hỏi người phụ trách LOCK.
- DB dev: Dev Test mang `github_username = hbob8686` (do gán thử, trước đó là `baohgse182460-bot`), nên đang là thành viên `Active` với lời mời GitHub còn treo.

**Nhận xét nhỏ:** lỗi nghiệp vụ như 409 "đã là thành viên" được ghi ở mức `fail` kèm stack trace trong log dù client nhận đúng mã.

### 3.4. Nhận feature và nhánh do bot tạo (Phần 4 của kế hoạch test, 2026-11-08)

Workspace "LOCK test - Nha Khoa" (đã Lock, 3 phiên bản snapshot), repo `shipmate-test-org/demo-app` (org, đã cài App). Chạy hai lượt: lượt đầu để tìm lỗi, lượt hai dọn sạch repo (chỉ còn `main`) và DB rồi chạy lại từ đầu. Mục này ghi kết quả lượt hai. Sau test đã đóng các PR, gỡ khóa và xóa mọi nhánh thử; DB dev đã xóa các bản ghi nhận feature thử.

**Các nhánh dùng trong lượt hai:**

| Mã | Nhánh | Người nhận | Dùng để thử |
|---|---|---|---|
| T1 | `feature/7e871cad-…-hbob8686` ("Đặt lịch khám online") | Dev `hbob8686` | Nhận khi chưa có repo rồi retry tạo nhánh; admin đẩy thẳng; xóa và force push |
| T2 | `feature/e5f4245d-…-hbob8686` ("Quản lý hồ sơ bệnh án") | Dev `hbob8686` | GitHub từ chối tạo nhánh lúc nhận rồi sửa và retry; Dev thật sửa file; PR vào nhánh khóa |
| T3 | `feature/c5b9ee73-…-24HBG` ("Đồng bộ lịch bác sĩ từ Outlook") | Manager `24HBG` | Tranh chấp nhận cùng lúc; nhận khi repo đã liên kết; retry khi nhánh đã có trên GitHub |
| P | `hbob8686-patch-1` (không khóa) | `hbob8686` | Nhánh GitHub tự tạo khi Dev bị chặn commit; nguồn của PR #2 và #3 |

- [x] **Nhận feature qua HTTP:** skill thấp hơn complexity 409 `SKILL_TOO_LOW_FOR_FEATURE`; feature không tồn tại 404; chưa có tên GitHub 400 `GITHUB_NOT_CONNECTED`; `POST …/branch` khi chưa liên kết repo 409 `WORKSPACE_REPO_NOT_LINKED`, bởi người không giữ feature 404; nhận feature đã có người giữ 409 `FEATURE_ALREADY_ASSIGNED`; hai người nhận cùng lúc (T3): Manager 201, Dev 409. Chưa kiểm được feature `backup` và người ngoài workspace (dữ liệu không có), đã có unit test.
- [x] **Ba cách tạo nhánh:** T1 nhận lúc chưa có repo (`branchCreated: false`) rồi retry sau khi có repo; T2 nhận khi GitHub từ chối (nhận vẫn thắng, retry báo `GITHUB_APP_UNINSTALLED`, sửa lại rồi retry thì tạo được); T3 nhận khi repo đã sẵn sàng (tạo nhánh ngay).
- [x] **Nhánh và quy tắc khóa (T1, T2, T3):** cả ba tạo đúng SHA đầu `main` (`071471f`); `enforce_admins: true`, không force push, không xóa nhánh, chỉ `shipmate-bot-dev` được đẩy (0 user, 0 team).
- [x] **Chặn xóa nhánh và force push thật:** xóa nhánh bị chặn trên T1, T2, T3; force push viết lại lịch sử bị chặn trên T1 và T3 (T2 chưa lệch `main` nên không thử được). Lưu ý: đẩy về đúng SHA hiện tại của nhánh thì GitHub báo thành công vì không làm gì, không phải lỗi khóa.
- [x] **Retry khi nhánh đã có trên GitHub mà DB chưa ghi nhớ (T3):** đi qua nhánh "đã tồn tại", đặt lại quy tắc, không lỗi.
- [x] **Dev thường bị chặn:** `hbob8686` (quyền write) sửa `README.md` trên nhánh T2 bằng giao diện web thì GitHub báo "You can't commit to … because it is a protected branch" và chỉ cho tạo nhánh mới (P). PR #2 từ P vào T2 hiện "Merging is blocked: You're not authorized to push to this branch": Dev không merge được vào nhánh khóa. Nhánh T2 không đổi.
- [x] **Cổng merge của `main` (PR #3 từ P vào `main`):** `mergeable_state: blocked`, 0 check run, check bắt buộc `ShipMate Scope Check` ở trạng thái "Expected". Thử merge bằng admin `24HBG` bị chặn: 405 "Required status check "ShipMate Scope Check" is expected."; `main` vẫn chỉ có commit đầu. Code chưa qua ShipMate không vào được `main`, kể cả admin.
- [x] **Snapshot không đổi:** hash 3 phiên bản (`1fce847a`, `369ca986`, `5db1469f`) giữ nguyên sau mọi thao tác. Audit trail của workspace này trống (Lock trước khi có audit trail), nên chưa kiểm chuỗi bản ghi trên workspace này.

**Lượt ba: sau khi sửa các lỗi trên (2026-11-08).** Chạy lại với cùng ba nhánh (T3 lần này do Dev `hbob8686` giữ vì thắng tranh chấp ngẫu nhiên: `feature/c5b9ee73-…-hbob8686`).

- [x] **Lỗi "admin bỏ qua khóa nhánh" đã sửa bằng GitHub Rulesets.** Khi tạo nhánh, bot tự tạo (nếu chưa có) một ruleset `ShipMate feature branch lock` cho mẫu `feature/**` với luật `creation`, `update`, `deletion`, `non_fast_forward`, **chỉ GitHub App của bot được bypass**. Không còn quy tắc bảo vệ riêng từng nhánh (`GET …/branches/{b}/protection` trả "Branch not protected"). Thử thật: admin `24HBG` bị chặn khi commit vào T1, T2, T3 (409 "Cannot update this protected ref"), xóa nhánh (422), tạo nhánh `feature/…` mới (422), `git push` thật bị từ chối ("push declined due to repository rule violations"); merge PR vào nhánh khóa bằng admin bị 405. Tạo nhánh ngoài mẫu (`staging/…`) vẫn được. Trước đó đã thử riêng (bước 0): bot tạo nhánh, commit, fast-forward thật và xóa nhánh khóa đều được nhờ bypass. Repo không dùng được ruleset (ví dụ repo riêng tư gói miễn phí, GitHub trả 403/404) thì quay về quy tắc riêng từng nhánh và vẫn không khóa cứng được.
- [x] **Giải phóng feature dọn nhánh:** `FeatureChallengeService` giờ nhờ bot xóa nhánh của feature bị thay thế (sau khi lưu việc duyệt, lỗi GitHub không hủy việc duyệt mà ghi audit `REPO_BRANCH_CLEANUP_FAILED`). Thử thật: duyệt `RemoveAndReplace` bỏ feature "Đồng bộ lịch bác sĩ" đang do Dev giữ, nhánh T3 biến khỏi GitHub, snapshot v4 được tạo, hash v1 đến v3 giữ nguyên, ghi một bản `SCOPE_CHANGE`. Challenge thử được tạo thẳng trong DB để khỏi gọi AI; nhánh của feature bị loại khỏi snapshot.
- [x] **Bỏ liên kết và đổi repo gỡ dấu vết của ShipMate:** gỡ ruleset và gỡ check `ShipMate Scope Check` khỏi `main`, giữ nguyên các quy tắc khác của chủ repo; lỗi chỉ ghi audit `REPO_CLEANUP_FAILED`, không chặn việc bỏ liên kết. Thử thật: sau khi bỏ liên kết ruleset về 0 và `main` không còn đòi check (PR vào `main` thành `clean`, mình không merge); lời mời repo còn treo của `hbob8686` bị hủy.
- [x] **Lỗi do chính phần sửa trên gây ra, đã sửa trong lượt này:** lần đầu gỡ check bằng `DELETE …/required_status_checks` làm `main` thành "được bảo vệ nhưng không có status checks", và `EnsureMergeGateAsync` không bật lại được cổng khi liên kết lại (`github_merge_gate_enabled = false`). Giờ chỉ làm trống danh sách check (`PATCH` với `checks: []`), nên liên kết lại thêm được check trở lại. Thử thật: bỏ liên kết (check rỗng, mục status checks vẫn còn), liên kết lại (check trở lại, gate bật, PR vào `main` lại `blocked`, admin merge bị 405).
- [x] **Token GitHub tự làm mới:** token hết hạn thì `GitHubAccessTokens` đổi bằng refresh token (GitHub cấp refresh token mới, lưu lại mã hóa); refresh token hết hạn, bị từ chối hoặc thiếu thì `GITHUB_NOT_CONNECTED`; GitHub không trả lời thì `GITHUB_REQUEST_FAILED`. Thử thật: đặt hạn token về quá khứ rồi liệt kê repo: 200, token và refresh token đều đổi, hạn mới +8 giờ; cả hai hết hạn: 400. Giới hạn: hai lần làm mới đồng thời có thể làm lần sau thất bại vì refresh token đã bị dùng (hiếm).
- [x] **Ngắt kết nối GitHub bị chặn với mọi thành viên `Active` của mọi workspace đang hoạt động** (không còn phụ thuộc workspace đã liên kết repo), vá kẽ hở "ngắt kết nối trước khi repo được liên kết". Giá: Dev đang trong workspace thì không ngắt kết nối được cho tới khi bị xóa hoặc workspace bị archive.
- [x] **Thông báo lỗi nhận feature khi chưa có GitHub** nay nói về nhánh (vẫn mã `GITHUB_NOT_CONNECTED`); `README.md` đã cập nhật cách chạy.
- [x] **Chặn cổng merge (lượt ba):** PR #5 từ `hbob8686-patch-1` vào `main` hiện "Some checks haven't completed yet", `ShipMate Scope Check` Required và Expected, nút merge bị khóa; admin merge bị 405. PR #4 vào nhánh khóa T2 cũng `blocked`, và admin merge bị 405 ("Repository rule violations found").
- **Ghi chú, chưa sửa: hai nơi định nghĩa "đã có GitHub" khác nhau.** Accept, nhận feature và mời vào repo kiểm `users.github_username`; setup check (`membersWithoutGitHub`) kiểm bản ghi kết nối OAuth trong `github_connections` (chưa bị thu hồi). Luồng thật ghi cả hai cùng lúc (đăng nhập hoặc kết nối GitHub) và ngắt kết nối xóa cả hai cùng lúc, nên không lệch. Chỉ lệch ở Dev Test vì tên GitHub `hbob8686` được gán thẳng vào DB (không qua OAuth): có tên nhưng không có bản ghi kết nối, nên Accept chạy được còn setup check luôn báo thiếu GitHub (`ready: false`). Thành viên không phải Manager thực ra chỉ cần tên GitHub, không cần token, nên nếu muốn nhất quán thì đổi setup check sang kiểm `github_username`; chưa làm vì chưa có đường nào của người dùng thật gây ra lệch này.

**Lỗi còn lại sau Phần 4:**
- **Chưa chạy thật:** Feature Challenge end to end với AI (đã thử duyệt bằng challenge tạo trong DB); feature `backup` và người ngoài workspace (chỉ có unit test); chuỗi audit trail cho workspace Lock từ đầu (workspace này Lock trước khi có audit trail, nên mới có bản `SCOPE_CHANGE` đầu tiên).
- **Nhánh còn lại sau khi bỏ liên kết:** bỏ liên kết không xóa các nhánh `feature/…` đang có trên repo (chúng chỉ mất khóa).
- **Phần 4 chưa test, hoãn sang các phần sau (2026-11-08, không chặn Phần 5):**

  | Việc chưa test | Khi nào làm |
  |---|---|
  | Feature Challenge đầy đủ với AI (gửi, chấm độ phức tạp, duyệt) rồi nhánh bị xóa khi feature bị thay thế | Phần 5 (đã có AI thật) |
  | Chuỗi audit trail từ lần Lock đầu (`SNAPSHOT_LOCKED`, `SCOPE_CHANGE`) | Phần 5, bằng một workspace mới đi DEFINE rồi LOCK (workspace "LOCK test - Nha Khoa" Lock trước khi có audit trail) |
  | Dev đẩy lên `staging/...` (nhánh không khóa) rồi bot fast-forward nhánh khóa | Phần 5 (submit và duyệt) |
  | Feature `backup` và người ngoài workspace | Khi có dữ liệu: nhận feature `backup` cần một Feature Challenge kiểu Backup; người ngoài cần thêm một tài khoản đã xác minh |
  | Repo cá nhân công khai: ruleset có khóa được không | Phần 7 (nhánh lỗi và hạ tầng), cần một repo cá nhân đã cài App |
- Ruleset chỉ có khi repo hỗ trợ; repo cá nhân riêng tư gói miễn phí vẫn không khóa cứng được. Chưa thử repo cá nhân công khai (có thể khóa được bằng ruleset).
- Các giới hạn đã chấp nhận ở mục 3.3 (archive không gỡ ai, dựa vào `github_username`, quyền cấp tay ngoài tầm kiểm soát, GitHub sự cố tạm chặn xóa thành viên).

### 3.5. Submit, phân tích, duyệt, Check Run, SignalR (Phần 5 của kế hoạch test, 2026-11-08)

Workspace "LOCK test - Nha Khoa", repo `shipmate-test-org/demo-app`, snapshot v4. AI thật: Gemini `gemini-3.5-flash-lite`, khoảng 7 lần gọi. Chưa có CLI `shipmate submit`, nên người giữ feature cho luồng chính là Manager `24HBG` đẩy lên `staging/…` bằng `git` (đúng người giữ feature nên hợp lệ); Dev `hbob8686` chỉ dùng cho các phép thử qua HTTP. Sau test đã đóng PR, xóa nhánh (nhánh `feature/…` xóa bằng bot vì ruleset chặn cả admin) và xóa submission, bản ghi nhận feature thử trong DB.

**Các nhánh dùng:**

| Mã | Nhánh | Người giữ | Dùng để thử |
|---|---|---|---|
| S1 | `feature/7e871cad-…-24HBG` và `staging/7e871cad-…-24HBG` | Manager `24HBG` | Đường chính, thay thế submission, duyệt một phần, FAST_FORWARD_FAILED, diff lớn |
| S2 | `feature/e5f4245d-…-hbob8686` | Dev `hbob8686` | `FEATURE_ASSIGNED_TO_OTHER`, duyệt bởi người không giữ feature. Không đẩy code |
| (không nhánh) | feature đã `replaced`, feature chưa ai giữ | | `FEATURE_REPLACED`, `FEATURE_NOT_ASSIGNED` |
| P | PR #6 `feature/7e871cad-…-24HBG` vào `main` | | Cổng merge sau khi duyệt (đóng, không merge) |

- [x] **Kiểm tra submit (HTTP):** `main` 400 `PROTECTED_BRANCH_SUBMIT`; `hotfix/x` 400 `HOTFIX_NOT_SUPPORTED`; tên nhánh sai mẫu hoặc featureId không có trong snapshot 400 `INVALID_FEATURE_ID`; feature đã thay thế 409 `FEATURE_REPLACED`; chưa ai giữ 409 `FEATURE_NOT_ASSIGNED`; nhánh của người khác 403 `FEATURE_ASSIGNED_TO_OTHER`; commit không phải đầu `staging/…` 400 `COMMIT_NOT_IN_STAGING`.
- [x] **Đường chính với AI thật:** Manager nhận feature (bot tạo nhánh khóa), đẩy một commit nhỏ đúng phạm vi ("chọn bác sĩ và khung giờ để đặt lịch") lên `staging/…`, submit 202 `Analyzing`; khoảng 9 giây sau `InScope`, confidence 1.0 so với ngưỡng 0.75, snapshot v4, không có gợi ý. Kết quả AI (`ai_model`, `ai_raw_response`) lưu trong `build_submissions`.
- [x] **Duyệt:** bot fast-forward `feature/…` tới đúng commit đã phân tích, xóa `staging/…`, ghi `approved_sha` và `last_approved_sha`; Check Run `ShipMate Scope Check` thành công do `shipmate-bot-dev` tạo trên commit đã duyệt.
- [x] **Cổng merge sau khi duyệt:** PR từ nhánh đã duyệt vào `main` có `mergeable_state: clean` (không merge để khỏi để lại commit trong `main`). Admin đẩy thẳng vào nhánh khóa sau khi bot đã fast-forward vẫn bị chặn (ruleset).
- [x] **Rule Engine bỏ qua AI (C1):** commit chỉ thêm một dòng comment, message "fix typo" thì `skipped_maintenance`, `carried_from_submission_id` trỏ tới submission đã duyệt, **không có thêm lần gọi Gemini nào** (đếm trong log).
- [x] **Thay thế submission (C2):** hai lần submit cùng một commit cách nhau 0,3 giây: lần đầu `superseded` (đang phân tích), lần sau `in_scope`.
- [x] **Điều kiện duyệt:** duyệt submission đã bị thay thế 409 `SUBMISSION_SUPERSEDED`; Dev không giữ feature duyệt 403 `NOT_FEATURE_HOLDER`; staging không nối tiếp nhánh feature (xuất phát từ `main`) 409 `FAST_FORWARD_FAILED`, nhánh feature giữ nguyên.
- [x] **Duyệt một phần theo commit (C7):** `commitSha` là commit A trong khi đầu staging là C: nhánh `feature/…` chỉ tiến tới A, `staging/…` còn nguyên (chưa tới đầu), Check Run tạo trên A, `approved_sha = A`.
- [x] **SignalR (Manager nghe hub `/hubs/workspace` trong lúc chạy):** nhận `AnalysisCompleted` (submissionId, featureId, status `InScope`, failureCode), `CodeScopeApproved` (kèm tên feature và `approvedSha`) và `BranchPushSuccess` (featureId, nhánh, sha). `OptimizationSuggested` chưa thấy vì AI không trả gợi ý; các sự kiện drift thuộc Phần 6.
- [x] **Bổ sung sau (cùng ngày), hai việc từng chưa test:** (1) duyệt ngay khi submission còn `Analyzing`: 409 `SUBMISSION_NOT_READY` ("This submission isn't waiting for approval"), trạng thái vẫn `Analyzing` ngay sau đó. (2) Sự kiện `OptimizationSuggested`: commit "quá tay" (event bus, strategy, factory và container chỉ để đặt một lịch hẹn) cho `InScope` confidence 1.0 kèm 1 gợi ý (bỏ các lớp thừa), và hub phát `AnalysisCompleted` rồi `OptimizationSuggested` (`suggestionCount: 1`) cùng lúc. Dùng nhánh S1 tạo lại từ `main`, một lần gọi Gemini; đã dọn nhánh và bản ghi thử.

**Lỗi phát hiện và đã sửa trong Phần 5:**
- **`DIFF_TOO_LARGE` bị lọt với file có diff lớn.** GitHub không trả `patch` cho file có diff quá lớn (`big-data.js` 2501 dòng thêm, `patch_present: false`), mà code đo kích thước bằng tổng độ dài `patch`, nên tổng chỉ còn vài nghìn ký tự và vượt qua giới hạn; AI nhận file không có nội dung, chỉ có tên file, rồi chấm `drift` 0.5 vì "tệp không liên quan". Sửa: `ChangedFile` có thêm `Changes` (dòng thêm cộng dòng xóa); file có `Changes > 0` mà không có `patch` bị coi là quá lớn (file nhị phân có `Changes = 0` vẫn bị bỏ qua như trước). Thêm 2 unit test (747/747 đạt). Chạy lại thật: submit đúng commit đó nay `failed` với `DIFF_TOO_LARGE` ngay, không tốn lần gọi Gemini nào.

**Lệch giữa tài liệu và code:** `03-build.md` mục GĐ 2 ghi kết quả AI log vào `ai_analysis_runs`; thực tế bảng đó chỉ dùng cho DEFINE và LOCK, còn BUILD lưu trên chính `build_submissions` (`ai_model`, `ai_raw_response`).

**Chưa test trong Phần 5:**
- Feature `backup` (`FEATURE_IS_BACKUP`) và Governance Suspended: Phần 7.
- Drift, Acknowledge, Override, ngưỡng và chuỗi audit: Phần 6.
- Phân bố điểm confidence của AI: Phần 8 (mọi commit nhỏ đúng phạm vi hôm nay đều ra 1.0, chưa đủ để kết luận).
- Merge PR thật vào `main` (cố ý không làm).

### 3.6. Drift, Acknowledge, Override, ngưỡng, chuỗi audit (Phần 6 của kế hoạch test, 2026-11-08)

Workspace "LOCK test - Nha Khoa" (repo `shipmate-test-org/demo-app`, snapshot v4) cho drift, Ack, Override; hai workspace mới tạo bằng HTTP không có repo cho phần ngưỡng và chuỗi audit từ lần Lock đầu (đã lưu trữ sau test). AI thật: Gemini, khoảng 10 lần gọi. Mọi commit thử đẩy bằng `git` của Manager `24HBG` (lên `staging/…` của cả hai feature, kể cả feature của Dev `hbob8686`, vì submit kiểm tra người giữ feature chứ không kiểm tra người đẩy). Sau test đã xóa các nhánh và xóa submission, cảnh báo, bản ghi nhận feature trong DB (bản ghi `audit_trail` giữ nguyên vì chỉ được thêm).

| Mã | Nhánh / workspace | Người giữ | Dùng để thử |
|---|---|---|---|
| D1–D3 | `feature/7e871cad-…-24HBG`, `staging/7e871cad-…-24HBG` | Manager | Drift lần 1 (Stripe), Ack, drift lần 2 (thêm chat) thay cảnh báo, rồi commit đúng phạm vi tự đóng cảnh báo |
| D4 | `feature/e5f4245d-…-hbob8686`, `staging/e5f4245d-…-hbob8686` | Dev | Ack bởi Dev, Override sai/đúng người, duyệt sau Override, commit sửa comment, drift lần 2 rồi Override thẳng từ `unresolved` |
| D6 | nhánh F1 thêm quảng cáo rồi thêm Stripe | Manager | Duyệt khi cảnh báo còn mở (chặn), sau Override (qua) |
| W2 | Workspace "Drift test W2" (không repo) | Manager | Chuỗi audit từ `SNAPSHOT_LOCKED` đầu tiên, ngưỡng hạ, Feature Challenge thật |
| W3, W4 | Workspace mới | Manager | Nâng ngưỡng (không ghi dilution), 2 và 8 lần đổi chạy cùng lúc |

- [x] **Drift sinh ra từ AI thật:** commit Stripe (thẻ, hoàn tiền, đăng ký) cho feature "Đặt lịch khám online": `Drift`, confidence 0.0, ngưỡng 0.75, snapshot v4, `findings` nêu đúng file `src/payments/stripeGateway.js` và lý do; cảnh báo `Unresolved` tạo cùng lúc. Manager và Dev đều thấy cảnh báo ở `GET .../alerts?status=unresolved`. Hub phát `AnalysisCompleted` rồi `DriftAlertTriggered`.
- [x] **Acknowledge:** Dev không giữ feature 403 `NOT_FEATURE_HOLDER`; người giữ feature 200 `Acknowledged` (hub phát `DriftAlertAcknowledged`); Ack lần hai 409 `INVALID_ALERT_TRANSITION`; alert không tồn tại 404; Ack sau khi đã `Overridden` 409.
- [x] **Cảnh báo mới thay cảnh báo cũ:** drift lần hai trên cùng feature: cảnh báo cũ (đã `Acknowledged`) thành `resolved` với `resolved_by_submission_id` = submission `drift` mới, cảnh báo mới `unresolved`; mỗi feature chỉ có một cảnh báo mở. Hub không phát sự kiện riêng cho việc đóng/thay cảnh báo.
- [x] **Tự đóng khi sửa đúng phạm vi:** commit đúng phạm vi sau drift: `in_scope` 1.0, cảnh báo thành `resolved` với `resolved_by_submission_id` = submission `in_scope`.
- [x] **Commit chỉ sửa comment sau Override (bỏ qua AI):** `skipped_maintenance`, `carried_from_submission_id` = submission đã duyệt, cảnh báo `overridden` không bị đụng.
- [x] **Override:** Dev 403 `WORKSPACE_PERMISSION_DENIED`; thiếu `reason`, `reason` toàn khoảng trắng, `reason` dài 1001 ký tự đều 400 `VALIDATION_FAILED`; Manager Override từ `Acknowledged` và thẳng từ `Unresolved` đều 200 `Overridden` (hub phát `ManagerOverrideExecuted`); Override lần hai 409. Mỗi lần Override ghi `SCOPE_BYPASS` (alert, feature, submission, lý do, điểm, ngưỡng, version); workspace nhiều thành viên không ghi `SOLO_OVERRIDE_LOGGED` (đúng).
- [x] **Cổng duyệt theo cảnh báo:** duyệt submission `Drift` khi cảnh báo `Unresolved` hoặc `Acknowledged` 409 `DRIFT_UNRESOLVED`; sau Override duyệt được (200, bot fast-forward, hub phát `CodeScopeApproved` và `BranchPushSuccess`). Manager duyệt hộ feature của Dev vẫn 403 `NOT_FEATURE_HOLDER`.
- [x] **Ngưỡng (workspace W2, W3, W4, Nha Khoa):** `GET` trả `value`, `changesUsed`, `maxChanges`, `history`; 0.60 và 0.90 400 `THRESHOLD_OUT_OF_RANGE` (kèm `details.min/max`); 0.75 khi đang là 0.75 400 `THRESHOLD_UNCHANGED`; 0.755 và `"abc"` 400 `VALIDATION_FAILED`; Dev (thành viên Nha Khoa) 403, người ngoài workspace 404. Hạ 0.75 → 0.70: 200, ghi `THRESHOLD_CHANGED` rồi `GOVERNANCE_DILUTION`; đổi lần hai ngay 409 `THRESHOLD_CHANGE_COOLDOWN` kèm `nextChangeAllowedAt` (+7 ngày); nâng 0.75 → 0.80 chỉ ghi `THRESHOLD_CHANGED`.
- [x] **Chạy cùng lúc:** 2 và 8 lần đổi song song vào workspace mới: **đúng một** lần thắng, các lần còn lại bị chặn (`THRESHOLD_CHANGE_COOLDOWN`), lịch sử chỉ có một dòng, chuỗi audit liền mạch.
- [x] **Chuỗi audit từ lần Lock đầu:** workspace mới đi hết DEFINE rồi Lock: `SNAPSHOT_LOCKED` là `sequence = 1` (`prev_hash` toàn số 0, hash trong payload khớp hash snapshot); sau đó `THRESHOLD_CHANGED`, `GOVERNANCE_DILUTION`, và Feature Challenge thật (AI đề xuất độ phức tạp 2, Manager xác nhận, `AddDirectly`) ghi `SCOPE_CHANGE`; hàm kiểm chứng báo liền mạch (4 bản ghi). Workspace Nha Khoa (Lock trước khi có AuditTrail) bắt đầu chuỗi từ `SCOPE_CHANGE` và sau 3 lần Override vẫn liền mạch (4 bản ghi).
- [x] **Phát hiện sửa dữ liệu:** trên bản ghi `THRESHOLD_CHANGED` của một workspace thử: sửa nội dung, sửa giờ, đổi người đều báo `CHUOI DUT tai sequence 1`; hoàn tác thì lại liền mạch. (Lúc thử mình từng ghi đè nhầm payload của cả hai bản ghi trong workspace thử đó nên chuỗi báo đứt; đã trả đúng payload và kiểm chứng lại liền mạch, lỗi nằm ở thao tác thử, không phải ở code.)
- [x] **Xóa bản ghi giữa chuỗi (bổ sung sau):** workspace Drift test W2 có 4 bản ghi liền mạch; xóa bản ghi `sequence = 3` thì hàm kiểm chứng báo `CHUOI DUT tai sequence 3`.
- [x] **`SOLO_OVERRIDE_LOGGED` qua HTTP (bổ sung sau):** gỡ Dev khỏi Nha Khoa (204) để Manager còn một mình, nhận feature, đẩy commit Stripe: AI thật cho `Drift` 0.0 và cảnh báo `Unresolved`. Manager Override thành công và chuỗi ghi `SCOPE_BYPASS` (#5) rồi `SOLO_OVERRIDE_LOGGED` (#6, payload chỉ có `alertId`); chuỗi 6 bản ghi vẫn liền mạch. Sau đó mời Dev lại: Accept 200 với `repoInvite: "sent"`; đã xóa nhánh và bản ghi thử. Lời mời cộng tác viên repo của `hbob8686` đang chờ chấp nhận trên GitHub nếu cần dùng lại.

**Phát hiện trong Phần 6 (đã giải quyết bằng luật R1 ở mục 3.9, 2026-11-08):**
- **AI chấm đủ ngưỡng dù chính nó nêu file ngoài phạm vi.** Commit thêm banner quảng cáo bên thứ ba cho feature đặt lịch khám: `confidence_score` 0.8 (ngưỡng 0.75) nên `InScope`, nhưng `findings` ghi "không thuộc phạm vi tính năng đặt lịch khám online", và submission duyệt được không cần Override (nhánh feature đã fast-forward tới code quảng cáo). Commit thanh toán Stripe hoặc ví Bitcoin thì chấm 0.0 nên bị bắt. Một lần chạy chưa đủ để kết luận về prompt; đưa vào Phần 8 (phân bố confidence). Có thể bổ sung luật: có `findings` thì không coi là `InScope`.
  - **Quyết định (2026-11-08): chưa sửa, để Phần 8 quyết dựa trên dữ liệu.** Mới có một mẫu lệch; nếu thêm ngay luật "có `findings` thì không `InScope`" mà AI hay để `findings` cho nhận xét nhẹ, hợp lệ, hệ thống sẽ báo drift oan nhiều hơn lỗi hiện tại. Chấp nhận rủi ro tạm thời vì đây là môi trường test.
  - **Việc cần làm ở Phần 8:** chạy khoảng 10 submission gồm ba loại: đúng phạm vi, lệch nhẹ (banner quảng cáo, trang phụ, analytics, đổi giao diện không liên quan) và lệch rõ (thanh toán, ví tiền số). Với mỗi lần ghi điểm, `findings`, có `InScope` hay không. Đếm riêng: (a) số lần có `findings` mà điểm vẫn ≥ ngưỡng (lỗi lọt), (b) số lần đúng phạm vi mà vẫn có `findings` (luật mới sẽ báo oan). Từ hai số này chọn: luật `findings` ≠ rỗng thì không `InScope`, chỉnh prompt, hoặc cả hai.
- **Gemini trả 503 ở lần gọi đầu của Phần 6** (`AI_UNAVAILABLE` khi DEFINE analyze); thử lại sau 20 giây thì được. Không phải lỗi code, chỉ ghi nhận.

**Chưa test trong Phần 6:**
- Mã `THRESHOLD_CHANGE_CONFLICT`: mọi lần chạy song song đều ra `THRESHOLD_CHANGE_COOLDOWN` vì bên thua đọc lại sau khi bên thắng đã lưu. Khóa lạc quan chỉ có unit test và lần thử PostgreSQL ở GĐ 6.
- Giới hạn 3 lần đổi (`THRESHOLD_CHANGE_LIMIT_REACHED`): cần 3 lần cách nhau 7 ngày, chỉ có unit test.
- Ngưỡng mới áp dụng cho submission kế tiếp qua HTTP (cần đổi ngưỡng của Nha Khoa, đã không làm để khỏi tiêu lượt đổi); cảnh báo cũ giữ `threshold_at_check` chỉ có unit test.
- Kiểm chứng chuỗi audit qua HTTP: chưa có endpoint, chỉ chạy hàm kiểm chứng trực tiếp trên DB.

### 3.7. Các ca lỗi và nhánh đặc biệt (Phần 7 của kế hoạch test, 2026-11-08)

Đã làm xong nhóm A (feature backup), E (người ngoài workspace), và B, C, D trên một repo cá nhân công khai (gỡ App, push lén, ruleset). Nhóm B làm trên App cài ở tài khoản cá nhân `24HBG`, không đụng cài đặt của org `shipmate-test-org`.

Workspace "LOCK test - Nha Khoa", snapshot v4 → v5. AI thật: Gemini, 2 lần (đề xuất độ phức tạp cho hai challenge). Không có nhánh `staging/…` nào được đẩy; chỉ có nhánh `feature/…` do bot tạo khi Dev nhận feature backup (đã xóa sau test).

- [x] **Tạo feature backup (A):** Dev gửi Feature Challenge "Đồng bộ lịch với hệ thống bệnh viện khác" (HL7/FHIR): AI đề xuất độ phức tạp 5, cao hơn skill của mọi thành viên (4), nên gợi ý `Backup`. Manager xác nhận 5, duyệt `Backup`: snapshot lên v5, feature có trạng thái `Backup`, `qualifiedMembers` rỗng.
- [x] **Không ai nhận được khi chưa đủ skill:** Manager và Dev nhận feature backup đều 409 `SKILL_TOO_LOW`. Dev submit vào nhánh của feature backup: 409 `FEATURE_IS_BACKUP`.
- [x] **Nhận feature backup sau khi đủ skill:** Dev nâng skill lên 5, nhận 201, bot tạo nhánh `feature/…-hbob8686`, feature chuyển `Locked` có người giữ. Trong snapshot v5 hash đổi tại chỗ (`5db1469f50…` trước, `7beadcc805…` sau) mà version vẫn là 5, đúng thiết kế (backup được khóa cùng version khi có người nhận); không có bản ghi audit riêng cho việc này.
- [x] **`BACKUP_NOT_APPLICABLE`:** challenge độ phức tạp 1, duyệt `Backup` khi có người đủ skill: 409 `BACKUP_NOT_APPLICABLE`; đã từ chối challenge đó.
- [x] **Người ngoài workspace (E):** tài khoản `outsider.test@example.com` (không phải thành viên). Mọi endpoint của workspace Nha Khoa đều 404 `NOT_FOUND` (không lộ workspace có tồn tại): chi tiết workspace, thành viên, nhận feature, cảnh báo, Ack, submit, xem submission, ngưỡng (đọc và đổi), snapshot, lock/check, setup, challenges, product definition, next-best-action, accept, liên kết repo. `GET /api/workspaces` trả `[]`. Không có token: 401. SignalR: kết nối được (có token hợp lệ) nhưng `JoinWorkspace` bị từ chối `WORKSPACE_NOT_FOUND`, nên không nhận sự kiện của workspace; token sai bị chặn ngay lúc negotiate (401).

**Phát hiện trong Phần 7 (chưa sửa, cần quyết):**
- **Đăng ký email mà dịch vụ gửi mail từ chối trả 500 (đã sửa, cùng ngày).** Đăng ký `outsider.test@example.com`: Resend trả 422 ("dùng địa chỉ test thay vì domain example.com") nên `POST /api/auth/register` trả 500 chung chung, dù tài khoản đã được tạo; đăng ký lại báo trùng email. Sửa: lỗi gửi mail xác minh (register và resend-verification) giờ trả 502 `EMAIL_DELIVERY_FAILED` với lời nhắn "tài khoản đã lưu, hãy xin mã mới"; `forgot-password` vẫn trả 200 và chỉ ghi log khi mail không gửi được (để không lộ email nào có tài khoản); `ResendEmailSender` ghi log lý do Resend từ chối (trước đây mất nội dung 422). Thêm 3 unit test (750/750 đạt). Chạy thật: register 502, đăng ký lại 409, resend 502, forgot-password 200, log hiện đúng lý do 422. Đã ghi vào `docs/frontend-guide.md` mục 2.7.

**Repo cá nhân công khai (nhóm C, D; `24HBG/shipmate-personal-test`, workspace thử W5, đã cài App):**
- [x] **Ruleset trên repo cá nhân công khai (D):** bot tạo được ruleset "ShipMate feature branch lock" (bypass chỉ có App `shipmate-bot-dev`). Chủ repo `24HBG` push vào `feature/…` bị chặn (`GH013`, "Cannot update this protected ref"), force-push và xóa nhánh cũng bị chặn: ruleset chặn cả chủ repo, khóa cứng đúng thiết kế. `main` có check bắt buộc `ShipMate Scope Check` với `enforce_admins`: chủ repo push thẳng vào `main` bị từ chối (`GH006`).
- [x] **Push lén vào nhánh feature (C1):** khi nhánh chưa được khóa, push thẳng một commit vào `feature/…` rồi submit: chuỗi audit ghi `DIRECT_PUSH_DETECTED` (`branch`, `featureId`, `headSha`, `expectedSha: null`); submit lại cùng nhánh không ghi trùng (vẫn 1 bản ghi).
- Không gây ra được: push lén vào `main` (C2) và PR không có ✅ rồi merge (C3), vì `main` khóa cả chủ repo; merge PR có ✅ (C4) không làm vì phải merge thật vào `main` của repo thử. Các ca này chỉ có unit test; cổng merge đã kiểm chứng ở mục 3.2 và 3.5.

**Hai điểm yếu tìm ra ở repo cá nhân, đã sửa (cùng ngày):**
- **Hủy liên kết rồi liên kết lại làm hở nhánh đã giao.** Hủy liên kết xóa ruleset, liên kết lại không tạo lại; ruleset chỉ được tạo khi có người nhận feature mới nên nhánh nhận từ trước hở (chủ repo push thẳng vào được, và đó là cách gây ra C1). Sửa: sau khi liên kết, `WorkspaceService` gọi lại `CreateProtectedBranchAsync` cho mọi bản ghi nhận feature còn hoạt động đã có nhánh (tạo ruleset nếu thiếu, hoặc branch protection từng nhánh khi không có ruleset). Nhánh khóa lỗi thì `BranchCreatedAt = null` để người giữ feature thử lại. Đổi sang repo khác thì nhánh được tạo trong repo mới và `LastApprovedSha`, `FlaggedHeadSha` của repo cũ bị xóa (tránh báo `DIRECT_PUSH_DETECTED` oan). Chạy thật: hủy liên kết (0 ruleset), liên kết lại (ruleset có lại, cả hai nhánh `protected`), chủ repo push vào `feature/…` bị chặn ngay.
- **Liên kết vào repo trống làm cổng merge tắt vĩnh viễn.** Repo chưa có commit thì không có nhánh chính để bảo vệ; GitHub trả 404, code coi đó là "gói miễn phí không có branch protection" và âm thầm trả `false`, nên `mergeGateEnabled` kẹt `false` (setup `ready: false`) tới khi hủy và liên kết lại. Sửa: kiểm tra repo có commit trước khi ghi gì, chưa có thì 409 `GITHUB_REPO_EMPTY` (không đổi gì trong workspace). Chưa chạy thật ca repo trống (cần thêm một repo trống vào cài đặt App); chỉ có unit test.
- Thêm 5 unit test (755/755 đạt). Đã ghi vào `docs/frontend-guide.md` (mục 2.3 và bảng mã lỗi 2.5).

**Gỡ App rồi cài lại (nhóm B; workspace thử W5, App cài ở tài khoản cá nhân):**
- [x] **App bị gỡ → Governance Suspended:** sau khi gỡ `shipmate-bot-dev` khỏi tài khoản cá nhân, submit trả 409 `GOVERNANCE_SUSPENDED` ("ShipMate lost access to this repository…"); `GET /setup` báo `governanceSuspended: true`; chuỗi audit ghi `SYSTEM_ALERT` (`reason: APP_UNINSTALLED`, `repository`, không có người thực hiện) và workspace lưu lý do `APP_UNINSTALLED`. Submit lần sau vẫn bị chặn; duyệt một submission đã `in_scope` cũng 409 `GOVERNANCE_SUSPENDED`. Nhận feature mới vẫn được (201) nhưng `branchCreated: false` vì bot không còn quyền tạo nhánh.
- [x] **Cài lại App → tự phục hồi:** cài lại App cho đúng repo, submit kế tiếp: cờ tự xóa (`governanceSuspended: false`, `ready: true`), `installationId` mới (`179845973`) được ghi, submit chạy bình thường (`in_scope` 1.0). Nhánh đã nhận lúc App vắng thử lại bằng `POST .../assignments/{featureId}/branch`: tạo thành công, `branchCreated: true`. Ruleset, check bắt buộc của `main` và khóa các nhánh vẫn còn nguyên sau khi cài lại. Sau khi cài, GitHub chuyển trình duyệt về Setup URL `/api/github/app/installed?installation_id=…&setup_action=install`; backend không có endpoint này nên trả 404 (vô hại, việc của frontend nếu muốn có màn "cài xong").
- **Lỗi tìm ra và đã sửa:** lần submit đầu tiên sau khi gỡ trả 502 `GITHUB_REQUEST_FAILED` thay vì `GOVERNANCE_SUSPENDED`. Token cài đặt được lưu tạm tới 1 giờ; GitHub từ chối token đó (401) sau khi gỡ, code chỉ xóa token rồi báo lỗi chung; chỉ lần gọi sau xin token mới và nhận 404 "App đã gỡ". Sửa: `GetBranchHeadShaAsync` gặp 401 thì xin token mới ngay rồi thử lại một lần (App gỡ → `GitHubAppUninstalledException` → Suspended ngay; chỉ hết hạn → thử lại thành công). Thêm 2 unit test (`GitHubAppServiceTests`, 757/757 đạt). Chưa chạy thật lại đường này (cần gỡ App thêm một lần nữa).
- Dọn: xóa mọi nhánh trong repo thử, bản ghi nhận feature, submission, và lưu trữ workspace W5 (repo `24HBG/shipmate-personal-test` giữ lại, bạn xóa tay nếu muốn).

**Chưa làm trong Phần 7:**
- Thông báo `BackupFeatureClaimed` cho người giữ feature đang đợi: workspace test không có feature phụ thuộc vào feature backup, nên chưa có ai nhận sự kiện; chỉ có unit test.

### 3.8. Các ca còn lại của Phần 7 (2026-11-08, sau mục 3.7)

Workspace thử W6 (repo cá nhân `24HBG/shipmate-personal-test`, App cài ở tài khoản cá nhân, Manager `24HBG` giữ feature), cùng các workspace rỗng cho phần ngưỡng. AI thật: Gemini, khoảng 12 lần gọi. Một công cụ thử chỉ dùng cho test (không nằm trong repo dự án) lùi giờ các bản ghi audit của một workspace thử rồi nối lại chuỗi hash bằng chính `AuditChain` của dự án, nên chuỗi vẫn hợp lệ (kiểm tra sau mỗi lần).

- [x] **Giới hạn 3 lần đổi ngưỡng:** workspace thử, lùi giờ giữa các lần: lần 1 (0.80) 200; lùi 6 ngày 23 giờ thì 409 `THRESHOLD_CHANGE_COOLDOWN`; lùi thêm tới hơn 7 ngày 1 giây thì được đổi (lần 2); lần 3 được; lần 4 409 `THRESHOLD_CHANGE_LIMIT_REACHED`; `GET` báo `changesUsed: 3`, `nextChangeAllowedAt: null`, lịch sử đủ 3 dòng; chuỗi audit 4 bản ghi liền mạch.
- [x] **`THRESHOLD_CHANGE_CONFLICT` gây ra được:** script Node mở sẵn kết nối rồi bắn 11 yêu cầu đổi ngưỡng cùng lúc vào mỗi workspace mới, 10 workspace: tổng 10 lần thành công (đúng **một** lần mỗi workspace), 93 lần `THRESHOLD_CHANGE_CONFLICT`, 7 lần `THRESHOLD_CHANGE_COOLDOWN`. Mỗi workspace chỉ có đúng 1 bản ghi `THRESHOLD_CHANGED`, chuỗi liền mạch. (Lần trước dùng nhiều tiến trình `curl` nên chỉ ra cooldown; chạy cùng một tiến trình với kết nối mở sẵn mới đủ sát.) Ca này vẫn **không có unit test**, chỉ có lần thử PostgreSQL ở GĐ 6 và lần chạy thật này.
- [x] **Ngưỡng mới áp dụng cho submission kế tiếp (qua HTTP):** W6 nâng 0.75 → 0.85. Các submission cũ giữ `threshold_at_check = 0.75`, submission sau đó 0.85; cảnh báo cũ (đã `resolved`) giữ 0.75, cảnh báo mới 0.85. Hiệu ứng trên kết quả cũng thấy: commit "xếp thợ theo đánh giá" từng được 1.0 ở ngưỡng 0.75, lần sau chấm 0.8 nên `Drift` ở ngưỡng 0.85 (và chỉ qua được sau khi Manager Override). Điểm AI không cố định giữa các lần chạy, nên đây là bằng chứng gián tiếp, không phải cùng một điểm ở hai ngưỡng.
- [x] **`RemoveAndReplace` end-to-end (AI thật):** feature bị thay có người giữ và nhánh; hai feature phụ thuộc vào nó (trực tiếp và gián tiếp). Challenge: AI đề xuất độ phức tạp 4 (Manager xác nhận 2) và kiểm tra persona (`personaConflict: false`). Duyệt thiếu cách xử lý phụ thuộc: 409 `DEPENDENTS_RESOLUTION_REQUIRED`; thiếu `removeFeatureId`: 400 `VALIDATION_FAILED`; id không có: 404; `Restructure`: 200, snapshot v2, feature cũ `replaced`, feature mới `locked`, phụ thuộc chuyển sang feature mới, bản ghi nhận của feature cũ được nhả và nhánh `feature/…` của nó bị xóa (kể cả khi có ruleset), `SCOPE_CHANGE` ghi `removedFeatureId`, hub phát `FeatureRemoved` kèm `dependentFeatureIds`, chuỗi 2 bản ghi liền mạch. Cách `RemoveDependents` chỉ có unit test.
- [x] **C4, merge PR có ✅ vào `main`:** commit đã duyệt (bot fast-forward, Check Run `ShipMate Scope Check` `success`): PR `CLEAN`. Merge thường rồi merge squash (hai PR): không có `DIRECT_PUSH_DETECTED`, `last_audited_main_sha` tiến tới commit merge mỗi lần.
- [x] **C2, push thẳng vào `main`:** gỡ branch protection của `main` trên repo thử (qua `gh`), chủ repo push một commit thẳng, submit: ghi `DIRECT_PUSH_DETECTED` (`branch: main`, `commitSha`, `reason: NO_PULL_REQUEST`), `last_audited_main_sha` cập nhật.
- [x] **C3, merge PR không có ✅:** cũng khi `main` chưa được bảo vệ, merge một PR từ nhánh `other/x` (không có Check Run): hai bản ghi `DIRECT_PUSH_DETECTED` với `reason: NO_SCOPE_CHECK` (commit của PR và commit merge).
- [x] **Khôi phục sau C2/C3:** hủy liên kết rồi liên kết lại W6: `setup` `ready: true`, `main` lại yêu cầu `ShipMate Scope Check`, chủ repo push vào `main` bị từ chối (`GH006`), ruleset có lại.

- [x] **Repo trống chạy thật (`GITHUB_REPO_EMPTY`):** repo riêng tư `24HBG/shipmate-empty-test` (chưa có commit), thêm vào App rồi liên kết vào W6: 409 `GITHUB_REPO_EMPTY`, W6 vẫn liên kết với repo cũ, không đổi gì. GitHub thật trả `409 "Git Repository is empty."` cho `GET /commits` trên repo trống, khớp với cách code nhận biết.
- [x] **Đường 401 → xin token mới, chạy thật:** nạp sẵn token cài đặt trong bộ nhớ (một submit lúc 20:16), bạn gỡ App, submit **lần đầu duy nhất** lúc 20:17: trả thẳng 409 `GOVERNANCE_SUSPENDED` (trước khi sửa là 502 `GITHUB_REQUEST_FAILED`), chuỗi audit ghi `SYSTEM_ALERT` (`APP_UNINSTALLED`), workspace `governance_suspended_at` có giá trị.
- [x] **Cài lại App sau ca 401:** submit kế tiếp tự xóa cờ Suspended (`setup` từ `ready: false` sang `ready: true`), `installationId` mới `179858946` được ghi, submission `in_scope`.
- Dọn: unlink W6 (ruleset bị gỡ), xóa nhánh `feature/…`, `staging/…`, `other/x` trong repo thử, xóa submission, cảnh báo và bản ghi nhận feature trong DB, lưu trữ W6, W7 và các workspace thử. Repo `24HBG/shipmate-personal-test` (công khai, `main` còn các commit thử) và `24HBG/shipmate-empty-test` (riêng tư, chỉ có README) giữ lại, bạn xóa tay nếu muốn.

**Phát hiện:**
- **`BackupFeatureClaimed` không thể xảy ra qua API.** Sự kiện chỉ gửi cho người giữ feature *phụ thuộc* vào feature backup vừa được nhận, nhưng feature backup chỉ sinh ra từ Feature Challenge (sau Lock) và `AddFeature` không tạo phụ thuộc nào; thêm phụ thuộc sau Lock bị từ chối (409 "The product definition is locked"). Nên không có feature nào đang đợi feature backup, và thông báo này hiện chưa tới được ai. Chỉ có unit test (tự dựng snapshot có phụ thuộc). **Đã quyết (chủ LOCK, 2026-11-09): giữ nguyên code, không sửa.** Cách xử lý feature backup vẫn là để nguyên ở `backup` và mời dev đủ skill (`BackupFeatureMatched`).
- **Điểm AI hai cực.** Bảy lần chấm ở phần này ra 0.0, 1.0 (ba commit "nhẹ": đổi ngôn ngữ, xếp thợ, đếm lượt xem trang) hoặc 0.8; không thấy điểm lưng chừng ổn định. Một commit đếm lượt xem trang (analytics gửi ra dịch vụ ngoài) được 1.0 `InScope`. Đưa vào Phần 8.
- **Liên kết repo riêng tư trên gói GitHub miễn phí trả 502 (đã sửa, cùng ngày).** Với repo riêng tư của tài khoản cá nhân gói miễn phí, GitHub trả 403 "Upgrade to GitHub Pro or make this repository public" khi đọc branch protection và ruleset. `EnsureMergeGateAsync` chỉ coi 404 là "không bảo vệ được" (ở bước đặt luật), còn bước đọc luật thì ném lỗi, nên `POST …/github/link` trả 502 `GITHUB_REQUEST_FAILED` thay vì liên kết với `mergeGateEnabled: false` như tài liệu mô tả. Gợi ý: coi 403 ở bước đọc của repo cá nhân giống 404. Sửa: coi 403 có lời nhắn "Upgrade to GitHub…" là "không bảo vệ được" ở cả `EnsureMergeGateAsync` (đọc luật) và `RemoveRepoLockAsync` (hủy liên kết cũng từng hỏng ở đúng chỗ này); 403 vì lý do khác vẫn là lỗi. Chạy thật: liên kết repo riêng tư gói miễn phí 200 với `mergeGateEnabled: false`, hủy liên kết 204.
- **Đổi sang repo khác mà bước giữa chừng lỗi để lại repo cũ không còn khóa (đã sửa, cùng ngày).** Khi chuyển từ repo A sang repo B, hệ thống gỡ ruleset và check bắt buộc của `main` ở A **trước** khi bật cổng ở B; B lỗi (502 ở trên) thì workspace vẫn trỏ A nhưng A đã mất khóa (thấy `rulesets = 0`, check của `main` = 0 trên GitHub). Đã liên kết lại A để khóa trở lại. Gợi ý: bật cổng ở B trước, gỡ khóa A sau cùng, để lỗi giữa chừng không đổi gì ở A. Sửa: `LinkGitHubRepoAsync` bật cổng ở repo mới **trước** khi gỡ thành viên và khóa ở repo cũ; repo mới từ chối thì repo cũ không bị đụng (unit test). Chạy thật: chuyển từ repo công khai (đang khóa, có nhánh nhận) sang repo riêng tư: 200, nhánh nhận được tạo lại ở repo mới (không khóa được vì gói miễn phí), repo cũ được gỡ ruleset và check của `main`. Thêm 4 unit test (761/761 đạt).
- **Gỡ branch protection của `main` là việc chủ repo làm được**, và hệ thống chỉ biết khi submit kế tiếp (ghi `DIRECT_PUSH_DETECTED`); không có cảnh báo riêng cho việc gỡ protection.

### 3.9. Chất lượng điểm AI (Phần 8, 2026-11-08): đo hiện trạng

Công cụ thử nằm ngoài repo, gọi đúng `BuildAnalysisClient.AnalyzeAsync` với Gemini thật (`gemini-3.5-flash-lite`) và prompt hiện tại, trên 17 ca (feature giữ: "Xem danh sách thợ và khung giờ trống"; hai feature khác trong MVP). **22 lần gọi** (giới hạn cứng 25). Nhãn kỳ vọng ghi trước khi chạy. Ở nhóm C, mỗi ca là phần code đúng phạm vi (danh sách thợ và khung giờ) **cộng thêm một file ngoài feature**, như một người làm lệch thật sự sẽ nộp.

| Ca | Nội dung | Nhãn | Điểm (mỗi lần chạy) | `findings` |
|---|---|---|---|---|
| A1 đến A6, B1 đến B3 (9 ca) | Đúng phạm vi: danh sách, khung giờ, test, service/route, thư viện ngày, hook cho feature đặt lịch, README/CI/.gitignore, sửa nhỏ `index.js` | InScope | 1.0 (cả 9) | 0 (cả 9) |
| C1 | Banner quảng cáo bên thứ ba | Drift | 0.85, 0.85 | 1 (đúng file) |
| C2 | Đếm lượt xem trang gửi ra dịch vụ ngoài | Drift | 0.90, 0.90 | 1 |
| C3 | Chuyển ngôn ngữ cả website | Drift nhẹ | 0.85, 0.85 | 1 |
| C4 | Xếp thợ theo đánh giá | Chấp nhận được | 0.85, 0.85 | 1 (AI coi là ngoài phạm vi) |
| C5 | Điểm tích lũy khách quen | Drift | 0.75, 0.75 | 1 |
| D1 | Thanh toán Stripe | Drift | 0.60 | 1 |
| D2 | Chat realtime | Drift | 0.70 | 1 |
| D3 | Tự cài đặt "lịch trong ngày của chủ tiệm" (feature khác) | Drift | 0.80 | 1, `suspected_feature_ids` đúng feature đó |

**Kết quả**
- **`findings` phân biệt hoàn hảo trong mẫu này:** mọi ca có code ngoài phạm vi đều có đúng 1 `findings` trỏ đúng file và lý do (9/9 lần chạy); mọi ca đúng phạm vi có 0 `findings` (0/9, kể cả ca có thư viện mới, hook, README/CI). Không có `suggestions` nào ở các ca đúng phạm vi.
- **Điểm số thì không:** file lạ chìm trong phần code đúng nên điểm chỉ rơi xuống 0.75 đến 0.9 dù nội dung lệch rõ (thậm chí D3, cài hẳn một feature khác, vẫn được 0.80). Thanh toán Stripe chỉ 0.60 và chat 0.70 vì còn phần code đúng đi kèm; trong các lần thử trước, Stripe nộp riêng một mình ra 0.0. Điểm phụ thuộc tỷ lệ code lệch trong diff, không phụ thuộc mức nghiêm trọng.
- **Chỉ xét điểm (luật cũ), số lần lệch rõ bị cho qua:** tính theo lần chạy 8/9 ở ngưỡng 0.70, 7/9 ở 0.75 (mặc định), 5/9 ở 0.80, 4/9 ở 0.85, 2/9 ở 0.90. Năm ca chạy hai lần cho điểm giống hệt nhau nên các con số trên đếm đôi; **tính theo ca độc lập (6 ca lệch rõ: C1, C2, C5, D1, D2, D3), ở ngưỡng 0.75 lọt 4/6 ca** (C5 lọt vì điểm đúng bằng 0.75 và luật là `< 0.75`). Không có ngưỡng nào trong khoảng Manager được phép (0.70 đến 0.85) bắt hết, nên chỉnh ngưỡng không giải quyết được.
- **Thêm luật "có `findings` thì không `InScope`" (R1), tính lại từ cùng dữ liệu:** bắt 9/9 lần lệch ở mọi ngưỡng; báo oan ở ca đúng phạm vi: **0/9**. Hai lần báo thêm là C4 (nhãn "chấp nhận được" của mình, nhưng chính AI nói là ngoài phạm vi). C3 ("lệch nhẹ") cũng bị bắt.
- **Ổn định trên cùng đầu vào:** 5 ca chạy hai lần cho điểm giống hệt nhau (chênh 0.00). Trong mẫu này không thấy dao động; lần trước thấy cùng một commit ra 1.0 rồi 0.8 nhưng giữa hai lần snapshot đã đổi (Feature Challenge `RemoveAndReplace` làm đổi danh sách feature khác và phụ thuộc), tức đầu vào của AI không giống hệt nhau; chưa chứng minh được đó là nguyên nhân duy nhất.

**Giới hạn của phép đo:** mẫu nhỏ (17 ca, 22 lần gọi), diff tổng hợp và ngắn (tối đa vài file), một feature, một ngôn ngữ (JavaScript, mô tả tiếng Việt), một mô hình. Đây là bằng chứng chứ không phải chứng minh. Chưa đo diff lớn (gần giới hạn 60000 ký tự) có thể làm AI liệt kê nhiều `findings` nhỏ vô hại, là chỗ luật R1 có thể báo oan nhiều hơn.

**Giới hạn lớn nhất (đo thêm sau khi cài R1): AI không nêu `findings` thì R1 không có gì để nghe.** Ba ca thêm vào bộ đo, ở đó code ngoài phạm vi nằm **trong cùng hàm** với code đúng thay vì file riêng (cả ba đều đúng nhãn "Drift"): M1 gửi beacon analytics trong `listBarbers` ra **1.0, không có `findings`** (lọt); M2 cộng điểm tích lũy trong `listBarbers` ra 0.8, có `findings` (bắt được); M3 giữ tiền thẻ Stripe trong `freeSlots` ra 0.8, có `findings` (bắt được). Cộng thêm một ca chạy thật (mục dưới): đếm lượt xem trang đặt lịch nộp kèm code đúng ra `in_scope` 1.0, không `findings`. Vậy **hành vi theo dõi/telemetry gửi ra ngoài là loại AI dễ bỏ sót**, và chỉ khi nó nằm trong file riêng với mô tả rõ thì mới được nêu (C2 bị bắt, M1 và ca thật không). Tổng 25 lần gọi bộ đo (đủ giới hạn cứng).

#### R1 đã cài (2026-11-08)

**Luật:** một submission là "ngoài phạm vi" khi `điểm < ngưỡng` **hoặc** AI nêu ít nhất một `findings`. Một chỗ duy nhất (`BuildSubmission.ExceedsScope`), dùng ở ba nơi: lúc phân tích (`Drift` hay `InScope`), lúc duyệt (`BuildApprovalPolicy`, kể cả submission bỏ qua AI vì chỉ sửa comment: nó sao chép `findings` của lần trước nên một điểm cao không che được) và dòng phán quyết trong Check Run. Điểm AI trả về vẫn lưu nguyên. Không đổi prompt, bảng hay lần gọi AI.

**Ý 2, cho frontend:** `GET .../build/alerts` và sự kiện `DriftAlertTriggered` có thêm `trigger` (`Findings`, `LowConfidence`, `Both`) và (ở `alerts`) `findings` (file và mô tả). `findings` lấy từ submission đã tạo cảnh báo (bất biến) nên không đổi cơ sở dữ liệu. Chi tiết ở `docs/frontend-guide.md` mục 2.9.

**Kiểm chứng:** 772/772 test đạt (thêm 11: `AFinding_MakesItDrift…`, `NoFinding_AndAScoreAboveTheThreshold_StaysInScope`, sự kiện có `trigger`, duyệt submission sao chép `findings` khi chưa Override và khi đã Override và khi `findings` rỗng, `trigger` của cảnh báo). Chạy thật 3 submission (Gemini thật, workspace Nha Khoa, repo `demo-app`; đã dọn sau test):
- Code đúng phạm vi **kèm banner quảng cáo**: điểm 0.80 (≥ 0.75), có `findings` đúng file và lý do: **`Drift`**, cảnh báo `Unresolved` với `trigger: "Findings"`; duyệt khi đó 409 `DRIFT_UNRESOLVED`. (Trước R1 đúng ca này được qua.)
- Code đúng phạm vi **kèm đếm lượt xem trang**: `in_scope` 1.0, không `findings`, cảnh báo cũ tự `resolved`: **lọt** (AI không nêu).
- Chỉ code đúng phạm vi: `in_scope` 1.0, duyệt được, Check Run `ShipMate Scope Check` `success`.

**Ghi chú dữ liệu cũ:** vì chính sách duyệt giờ đọc cả `findings`, một submission lưu trước R1 mà `InScope` có `findings` sẽ cần Override mà không có cảnh báo nào để Override. Không có dữ liệu như vậy (BUILD chưa chạy thật, dữ liệu thử đã xóa) nên không viết đoạn chuyển đổi.

**Còn chưa làm, kèm điều kiện làm:**
| Ý | Khi nào làm |
|---|---|
| Gửi AI danh sách "điều Manager đã chấp nhận" để khỏi báo lại | Khi thấy cùng một thứ đã Override bị báo lại ở hai lần nộp liên tiếp trở lên trong dùng thật. Rủi ro: mở đường cho code đã chấp nhận phình ra |
| Quy tắc tĩnh không phụ thuộc AI cho telemetry (ví dụ diff thêm một địa chỉ `https://` tới host lạ) | Đo cho thấy bỏ sót telemetry; bài đo này đã thấy 2 lần lọt (M1 và ca chạy thật). Dễ báo oan với lời gọi API hợp lệ nên cần danh sách host cho phép |
| Chỉnh prompt để điểm khớp `findings`; trường `severity` cho `findings`; giữ bộ ca làm kiểm tra hồi quy trong repo | Khi cần (frontend không hiển thị ổn bằng `trigger`; R1 báo oan nhiều; đổi model Gemini) |

**Cách nói đúng về cổng này (cho báo cáo):** cổng phạm vi bằng AI là một lớp phòng thủ theo xác suất. Nó mạnh với code lệch thành chức năng rõ ràng (thanh toán, chat, điểm tích lũy, quảng cáo, cài hẳn feature khác) và yếu với những thứ trông vô hại như đếm lượt xem hay telemetry. Phần đảm bảo cứng là các lớp không dùng AI: khóa nhánh `feature/**` bằng Ruleset, bot chỉ fast-forward đúng commit đã phân tích, merge gate (cần repo hỗ trợ), phát hiện push lén và chuỗi audit.

#### Gợi ý làm code gọn hơn (`suggestions`): đo 2 ca (2026-11-08)

Cùng công cụ, 2 lần gọi thêm (tổng 27 lần, giới hạn cứng đã được nâng từ 25 lên 27 theo duyệt của chủ dự án). Cả hai ca đều nằm trong phạm vi feature.

| Ca | Nội dung | Điểm | `findings` | `suggestions` |
|---|---|---|---|---|
| S1 | Code rối: factory và strategy chỉ có một cách, repository bọc mảng với một hàm, thư viện `moment` cho vài phép tính giờ | 1.0 | 0 | **2**: bỏ `SlotStrategyFactory`; bỏ `BarberRepository`. Mỗi gợi ý có `problem`, `suggestion`, `reason` đúng lược đồ |
| S2 | Code gọn, một hàm tiện ích `time.js` được dùng lại ở nhiều nơi | 1.0 | 0 | **0** (không gợi ý oan) |

**Kết luận:** chức năng chạy được và phân biệt đúng trên cặp ca này: gợi ý khi code dư lớp, im lặng khi code gọn. Gợi ý cụ thể, nêu file và lý do.

**Giới hạn:** chỉ 2 ca nên là bằng chứng nhỏ, không phải chứng minh. AI **chưa nêu hết**: ở S1 nó không gợi ý bỏ thư viện `moment` (prompt có nêu loại này) và không gợi ý gộp `DefaultSlotStrategy` vào hàm. Prompt dặn "ít mà rõ" nên bỏ sót một phần là đúng thiết kế; đừng hứa với người dùng là gợi ý sẽ đầy đủ. Gợi ý chỉ hiện khi submission `InScope` (sau R1, ca có `findings` là `Drift` nên không hiện gợi ý và không bắn `OptimizationSuggested`).

#### Diff lớn (2026-11-08)

**Giới hạn `DIFF_TOO_LARGE` (đọc từ code, không phải suy đoán).** Mỗi lần nộp được so với `main`, nên diff là toàn bộ code của nhánh chưa vào `main`, không chỉ phần mới. Nếu tổng patch vượt 60000 ký tự (cấu hình `Build:MaxDiffCharacters`), hoặc GitHub cắt bớt danh sách file, hoặc có file thay đổi nhưng không có patch, submission dừng ở `Failed` với `DIFF_TOO_LARGE` và không gọi AI. Hệ thống không cắt bớt rồi chấm phần còn lại, nên code lệch không thể trốn ở phần đuôi. Hệ quả: một feature mà tổng code vượt khoảng 1500 dòng không thể phân tích hay duyệt, và nộp nhỏ hơn không giúp được vì diff cộng dồn. Cách xử lý hiện có: chia feature nhỏ hơn ở bước LOCK, hoặc quản trị server nâng ngưỡng. Chưa có chia diff tự động.

**Đo ca lớn đúng phạm vi (L1, 1 lần gọi, tổng 28/28):** 12 file, 42400 ký tự patch (code, test và dữ liệu mẫu 150 thợ), toàn bộ thuộc feature "Xem danh sách thợ và khung giờ trống". Kết quả: điểm 1.0, **0 `findings`** (không báo oan), 1 `suggestions` hợp lý (bảy hàm theo từng ngày trong tuần nên gom lại, đúng điều cố ý để lại trong mẫu). Vậy ở cỡ này R1 không bị `findings` rác như lo ngại. Giới hạn: một ca, một feature, dữ liệu mẫu chiếm phần lớn dung lượng; chưa đo diff gần sát 60000 ký tự toàn là logic, và chưa đo diff lớn có đoạn lệch nhỏ chôn giữa (AI có còn nhìn ra không).

## 4. Giới hạn đã biết (đã ghi ở mục 10 của 03-build.md)

- Repo cá nhân không khóa cứng được (không có branch protection).
- Khóa nhánh feature dùng GitHub Rulesets (xem mục 3.4), nên chặn cả admin; repo không hỗ trợ ruleset (riêng tư gói miễn phí) quay về quy tắc riêng từng nhánh và không khóa cứng được.
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
