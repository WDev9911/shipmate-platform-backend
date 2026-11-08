# Hướng dẫn cho Frontend (đọc cả bởi AI của FE)

Cập nhật: 2026-11-07, nhánh `feature/build`. File này gom những gì Frontend cần biết khi nối với backend ShipMate: **thay đổi API gần đây**, **luồng GitHub** và **cách chạy GitHub ở máy local**. Chi tiết nội bộ của từng luồng nằm ở `docs/flows/` (không bắt buộc đọc).

> **Ghi chú cho AI đang đọc file này.** Khi viết code Frontend:
> 1. Dựa vào `errorCode` (chuỗi cố định) để quyết định hiển thị, **không** dựa vào `title` hay `detail` (câu chữ có thể đổi).
> 2. Không tự đoán tên trường. Dùng đúng tên ở các bảng dưới (JSON là camelCase).
> 3. Không ghi secret (`ClientSecret`, khóa riêng của GitHub App, `Resend` key) vào code hay vào file nào được commit.
> 4. Các lời gọi cần cookie (đăng nhập, refresh, GitHub) phải dùng `credentials: 'include'`.

---

## 1. Thông tin chung

| Mục | Giá trị |
|---|---|
| API local (HTTPS, dùng cái này) | `https://localhost:7138` |
| API local (HTTP, chỉ để thử nhanh, không dùng cho GitHub) | `http://localhost:5140` |
| Swagger | `https://localhost:7138/swagger` |
| Origin của Frontend (CORS chỉ cho phép đúng cái này) | `http://localhost:3000` (cấu hình `App:FrontendBaseUrl`) |
| Xác thực | `Authorization: Bearer <accessToken>`; refresh token nằm trong cookie httpOnly |
| SignalR hub | `/hubs/workspace` (token gửi qua query `access_token`) |

**Định dạng lỗi** (RFC 7807, mọi lỗi nghiệp vụ đều có `errorCode`):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "Mô tả bằng tiếng Anh, có thể hiển thị tạm.",
  "instance": "/api/workspaces/<id>/...",
  "errorCode": "GITHUB_DISCONNECT_BLOCKED_BY_LINKED_WORKSPACES",
  "details": { "installUrl": "..." }
}
```

- `details` chỉ có ở một số lỗi (ví dụ `GITHUB_APP_NOT_INSTALLED` có `installUrl`).
- **Mới:** `title` giờ theo mã HTTP (409 là `Conflict`, 403 là `Forbidden`, 502 là `Bad Gateway`; 400 vẫn là `Bad request`). Trước đây mọi lỗi đều là `Bad request`.
- Lỗi nhập sai (400) có `errorCode: "VALIDATION_FAILED"` và `errors` theo từng trường.

---

## 2. Thay đổi API cần Frontend xử lý

### 2.1. Chấp nhận lời mời vào workspace (có thay đổi hành vi)

`POST /api/workspaces/{id}/members/accept` với body `{ "skillLevel": 1..5 }`.

**Mới:** người dùng **phải đã kết nối GitHub** mới chấp nhận được, vì thành viên được tự mời vào repo của workspace.

| Trường hợp | Kết quả |
|---|---|
| Chưa kết nối GitHub | `400`, `errorCode: "GITHUB_REQUIRED_TO_JOIN"`. Lời mời **vẫn ở trạng thái `Invited`** |
| Đã kết nối GitHub | `200`, trả thành viên với trường mới `repoInvite` |

**Việc FE cần làm:** khi nhận `GITHUB_REQUIRED_TO_JOIN`, chuyển người dùng sang bước kết nối GitHub (mục 3.2), xong quay lại cho họ bấm chấp nhận lại. Người đăng nhập bằng GitHub luôn đã có GitHub nên không gặp lỗi này; chỉ người đăng ký bằng email và mật khẩu mới gặp.

**Trường mới `repoInvite`** (chỉ có trong phản hồi của Accept, các nơi khác là `null`):

| Giá trị | Ý nghĩa | Gợi ý hiển thị |
|---|---|---|
| `sent` | Đã gửi lời mời vào repo trên GitHub | "Hãy chấp nhận lời mời tại github.com/<owner>/<repo>/invitations" (người dùng phải bấm chấp nhận bên GitHub) |
| `alreadyHasAccess` | Người này đã có quyền vào repo | Không cần nói gì |
| `noRepo` | Workspace chưa liên kết repo | Không cần nói gì; khi Manager liên kết repo, họ sẽ được mời |
| `failed` | Mời thất bại (GitHub lỗi hoặc từ chối) | "Chưa mời được vào repo, nhờ Manager thêm tay" |

Chấp nhận thành công **không bao giờ** bị chặn vì GitHub lỗi: chỉ `repoInvite` là `failed`.

### 2.2. Xóa thành viên

`DELETE /api/workspaces/{id}/members/{userId}` (chỉ Manager), **tham số mới** `?force=true`.

**Mới:** khi xóa, backend gỡ người đó khỏi repo trên GitHub **trước**, rồi mới đổi trạng thái.

| Kết quả | Ý nghĩa |
|---|---|
| `204` | Đã gỡ khỏi repo (nếu có) và đã xóa khỏi workspace |
| `502`, `GITHUB_REVOKE_FAILED` | Chưa gỡ được quyền trên repo. **Chưa xóa gì**, thành viên vẫn ở lại. Cho Manager thử lại |
| `404` | Người đó không còn là thành viên đang hoạt động |

Với `?force=true`, backend xóa **dù GitHub lỗi** và ghi lại dấu vết trong audit trail. Chỉ nên hiện nút này **sau khi** `GITHUB_REVOKE_FAILED`, kèm cảnh báo: "Người này có thể vẫn còn quyền vào repo; hãy gỡ tay trên GitHub".

### 2.3. Liên kết, bỏ liên kết, đổi repo

| API | Thay đổi |
|---|---|
| `POST /api/workspaces/{id}/github/link` `{ "owner", "name" }` | Sau khi liên kết, mọi thành viên đang hoạt động (trừ Manager) **được mời vào repo**. Nếu workspace đang liên kết repo khác, các thành viên bị gỡ khỏi repo cũ trước; lỗi thì **giữ nguyên liên kết cũ** (`GITHUB_REVOKE_FAILED`) |
| `DELETE /api/workspaces/{id}/github` | Gỡ mọi thành viên (trừ Manager) khỏi repo trước rồi mới bỏ liên kết. Có thể trả `502 GITHUB_REVOKE_FAILED`, cho thử lại |

**Liên kết lại khóa lại nhánh đã giao:** sau khi liên kết (kể cả liên kết lại cùng repo, hoặc đổi sang repo khác), backend khóa lại mọi nhánh `feature/…` đã giao cho người giữ feature. Nhánh nào khóa lỗi thì `branchCreated` về `false` ở `GET .../assignments`, cho người giữ feature bấm thử lại (`POST .../assignments/{featureId}/branch`). Khi đổi sang repo khác, nhánh được tạo mới trong repo mới từ nhánh chính của repo đó.

Bỏ liên kết (và đổi sang repo khác) gỡ phần ShipMate đã đặt trên repo cũ: bộ khóa nhánh `feature/**` và check bắt buộc `ShipMate Scope Check` của nhánh chính. Các quy tắc khác của chủ repo giữ nguyên. Nếu việc dọn này thất bại thì việc bỏ liên kết vẫn thành công; chủ repo dọn tay (ghi lại trong audit).

### 2.4. Ngắt kết nối GitHub

`DELETE /api/auth/github/connection`. **Mới:** bị chặn (`409`, `GITHUB_DISCONNECT_BLOCKED_BY_LINKED_WORKSPACES`) khi người đó là **thành viên đang hoạt động của bất kỳ workspace nào** đang hoạt động, kể cả workspace chưa liên kết repo (vì thành viên được mời vào repo bằng tên GitHub). Cần rời workspace, hoặc workspace phải được archive, trước khi ngắt kết nối. Các lỗi khác đã có: `GITHUB_DISCONNECT_REQUIRES_PASSWORD` (tài khoản chưa có mật khẩu).

### 2.5. Bảng mã lỗi liên quan GitHub

| `errorCode` | HTTP | Khi nào | FE nên làm |
|---|---|---|---|
| `GITHUB_NOT_CONNECTED` | 400 | Chưa kết nối GitHub, **hoặc token hết hạn mà không làm mới được** (backend tự làm mới bằng refresh token; chỉ khi refresh token hết hạn hoặc bị thu hồi mới phải kết nối lại), khi liệt kê hoặc liên kết repo; cũng khi nhận feature mà chưa có tên GitHub | Cho nút "Kết nối lại GitHub" (mục 3.2) |
| `GITHUB_REQUIRED_TO_JOIN` | 400 | Accept lời mời khi chưa có GitHub | Dẫn qua bước kết nối GitHub rồi quay lại |
| `GITHUB_ACCOUNT_ALREADY_LINKED` | 409 | Tài khoản GitHub này đã gắn với người dùng khác | Báo rõ, không thử lại được bằng tài khoản này |
| `GITHUB_REPO_NOT_ACCESSIBLE` | 403 | Manager không có quyền với repo đã chọn | Báo lỗi |
| `GITHUB_APP_NOT_INSTALLED` | 409 | Chưa cài GitHub App của ShipMate lên repo. `details.installUrl` là link cài | Mở `installUrl`, cài xong cho liên kết lại |
| `GITHUB_REPO_EMPTY` | 409 | Liên kết repo chưa có commit nào (không có nhánh chính để bảo vệ). Không đổi gì | Nhắn tạo một commit đầu tiên (README là đủ) rồi liên kết lại |
| `GITHUB_APP_UNINSTALLED` | 409 | App đã bị gỡ khỏi repo | Hướng dẫn cài lại |
| `GITHUB_REVOKE_FAILED` | 502 | Không gỡ được quyền trên repo | Cho thử lại (hoặc `?force=true` ở mục 2.2) |
| `GITHUB_REQUEST_FAILED` | 502 | GitHub không trả lời đúng | Cho thử lại |
| `GITHUB_DISCONNECT_BLOCKED_BY_LINKED_WORKSPACES` | 409 | Đang là thành viên workspace có repo | Giải thích; cần rời hoặc nhờ Manager bỏ liên kết |
| `GITHUB_DISCONNECT_REQUIRES_PASSWORD` | 400 | Tài khoản không có mật khẩu | Yêu cầu đặt mật khẩu trước |
| `GITHUB_EMAIL_UNAVAILABLE` | 400 | Đăng nhập GitHub nhưng GitHub không trả email xác minh | Báo lỗi |

### 2.6. Kiểm tra cài đặt BUILD

`GET /api/workspaces/{id}/setup` (chỉ Manager). Phản hồi:

```json
{
  "ready": false,
  "repoLinked": true,
  "appInstalled": true,
  "mergeGateEnabled": true,
  "repoIsOrganization": true,
  "governanceSuspended": false,
  "membersWithoutSkill": ["<userId>"],
  "membersWithoutGitHub": ["<userId>"]
}
```

**Repo riêng tư của tài khoản cá nhân trên gói GitHub miễn phí** liên kết được nhưng `mergeGateEnabled: false` (GitHub không cho đặt luật bảo vệ nhánh ở gói đó), nên `ready` sẽ là `false`: hiện cảnh báo "nhánh chính và nhánh feature chưa khóa được, nâng gói hoặc chuyển repo công khai". Nhánh feature vẫn được tạo, chỉ không được khóa.

`repoIsOrganization: false` không chặn `ready`, chỉ nên hiện cảnh báo: repo cá nhân thì khóa `main` yếu hơn. Với quy tắc mới (Accept cần GitHub), `membersWithoutGitHub` chủ yếu bắt người đã ngắt kết nối sau khi vào.

### 2.7. Đăng ký và gửi lại mã xác minh khi mail không gửi được

`POST /api/auth/register` và `POST /api/auth/resend-verification` trước đây trả `500` chung chung khi dịch vụ gửi mail từ chối (địa chỉ không hợp lệ, hoặc môi trường dev chỉ gửi được tới email chủ tài khoản Resend). Giờ:

| Tình huống | Kết quả | Việc Frontend làm |
|---|---|---|
| Mail gửi được | Như cũ | Chuyển sang màn nhập mã |
| Mail không gửi được | `502`, `errorCode: "EMAIL_DELIVERY_FAILED"`. **Tài khoản vẫn được tạo** (chưa xác minh) | Chuyển sang màn nhập mã kèm thông báo "chưa gửi được mail" và nút **Gửi lại mã** (`resend-verification`). **Không** cho đăng ký lại: sẽ báo `EMAIL_ALREADY_EXISTS` (409) |
| `forgot-password` mà mail không gửi được | Vẫn `200` như mọi trường hợp | Không đổi (cố ý im lặng để không lộ email nào có tài khoản) |

### 2.8. Trang "đã cài GitHub App" (Frontend tự làm, backend không có endpoint)

Sau khi người dùng cài GitHub App của ShipMate (`https://github.com/apps/<slug>/installations/new`, backend trả sẵn link này trong `details.installUrl` của lỗi `GITHUB_APP_NOT_INSTALLED`), GitHub **chuyển trình duyệt** về "Setup URL" đã cấu hình trong App, kèm query:

```
<SetupURL>?installation_id=179845973&setup_action=install
```

- `setup_action` là `install` (cài mới) hoặc `update` (đổi quyền hoặc đổi danh sách repo của bản cài sẵn có).
- **Backend không có endpoint cho đường dẫn này** (đang trả 404 nếu Setup URL trỏ vào backend, ví dụ `/api/github/app/installed`). Đó là trang cho người dùng, nên thuộc về frontend.
- **Việc cần làm:**
  1. Đặt Setup URL của App (GitHub → Settings → Developer settings → GitHub Apps → App → mục *Post installation*) trỏ tới **một route của frontend**, ví dụ `/github/installed`. Nhờ người giữ App cấu hình, môi trường dev và production dùng App khác nhau nên URL khác nhau.
  2. Route đó hiện thông báo "Đã cài xong" và đưa người dùng về **màn liên kết repo** của workspace (mục 3.3) để bấm liên kết lại.
  3. **Không cần gửi `installation_id` cho backend.** Khi liên kết, backend tự tìm bản cài theo `owner/repo` (`POST /api/workspaces/{id}/github/link`). Setup URL chỉ có một giá trị cho cả App nên không mang được id workspace; nếu cần quay lại đúng workspace, lưu id workspace ở frontend (ví dụ `sessionStorage`) trước khi mở link cài.
- **Sau khi cài lại App cho workspace đang bị `governanceSuspended`:** không cần làm gì thêm ở backend. Lần submit kế tiếp tự xóa cờ và cập nhật `installationId`. Frontend có thể gọi `GET /api/workspaces/{id}/setup` sau khi quay lại, nhưng cờ chỉ tự xóa ở lần submit kế tiếp, nên màn hình nên báo "hãy submit lại để tiếp tục". Nhánh feature nhận lúc App vắng (`branchCreated: false`) thử lại bằng `POST /api/workspaces/{id}/assignments/{featureId}/branch`.

### 2.9. Cảnh báo Drift có thêm lý do (`trigger`) và danh sách `findings`

Từ nay một submission bị `Drift` khi **điểm thấp hơn ngưỡng** *hoặc* **AI nêu ít nhất một đoạn code ngoài phạm vi** (`findings`), kể cả khi điểm cao. Nên cảnh báo có thể mang điểm cao hơn ngưỡng (ví dụ điểm 0.85, ngưỡng 0.75); đừng chỉ hiện hai con số đó.

`GET /api/workspaces/{id}/build/alerts` (và các phản hồi Acknowledge, Override) trả thêm:

| Trường | Ý nghĩa |
|---|---|
| `trigger` | `"Findings"`: có code ngoài phạm vi (điểm có thể cao). `"LowConfidence"`: điểm thấp, AI không nêu gì cụ thể. `"Both"`: cả hai |
| `findings` | `[{ "file", "description" }]`: file và lý do AI cho là ngoài phạm vi. Rỗng khi `trigger` là `LowConfidence` |

Sự kiện SignalR `DriftAlertTriggered` có thêm `trigger` (không kèm `findings`; lấy danh sách qua `GET .../alerts`).

**Gợi ý hiển thị:** khi `trigger` là `Findings` hoặc `Both`, hiện "Có code ngoài phạm vi" kèm từng file và mô tả; chỉ khi `LowConfidence` mới nhấn mạnh điểm và ngưỡng. Ngưỡng mà Manager chỉnh (0.70 đến 0.85) giờ chỉ quyết định các ca AI chấm thấp mà không nêu `findings`.

**Lưu ý với người dùng:** cổng phạm vi bằng AI mạnh với code lệch rõ nhưng có thể bỏ sót thứ trông vô hại (ví dụ đếm lượt xem trang). Không nên hứa với người dùng rằng "mọi thứ ngoài phạm vi đều bị chặn".

---

## 3. Luồng GitHub (OAuth) cho Frontend

Hai luồng dùng **cùng một GitHub OAuth App và cùng callback**:

### 3.1. Đăng nhập bằng GitHub

1. FE gọi `GET https://localhost:7138/api/auth/github/login-url` với `credentials: 'include'`. Nhận `{ "url": "https://github.com/login/oauth/authorize?..." }`.
2. FE điều hướng cả trang (`window.location.href = url`). **Không** mở trong iframe, **không** dùng popup chặn cookie.
3. Người dùng bấm Authorize trên GitHub. GitHub gọi `https://localhost:7138/api/auth/github/callback`, rồi backend chuyển hướng về FE:
   - Thành công: `http://localhost:3000/oauth-callback?handoff=<mã dùng một lần>`
   - Lỗi: `http://localhost:3000/login?error=<errorCode>` (ví dụ `invalid_state`, `GITHUB_EMAIL_UNAVAILABLE`)
4. Trang `/oauth-callback` đọc `handoff` và gọi `POST /api/auth/github/exchange` với body `{ "handoffCode": "<mã>" }` và `credentials: 'include'`. Nhận cùng dạng dữ liệu như `login` (`accessToken`, ...) và cookie refresh token được đặt.

### 3.2. Kết nối (hoặc kết nối lại) GitHub cho tài khoản đang đăng nhập

1. Gọi `GET /api/auth/github/connect-url` với `Authorization: Bearer ...` **và** `credentials: 'include'`. Nhận `{ "url": ... }`.
2. Điều hướng cả trang tới `url`.
3. Callback chuyển hướng về FE:
   - Thành công: `http://localhost:3000/settings?github=connected`
   - Lỗi: `http://localhost:3000/settings?error=<errorCode>` (ví dụ `GITHUB_ACCOUNT_ALREADY_LINKED`)
4. FE hiển thị kết quả dựa vào query này rồi xóa query khỏi URL.

Lưu ý: nếu người dùng đã cấp quyền cho ShipMate trước đó, GitHub **tự chấp nhận**, không hiện màn hình Authorize. Việc kết nối lại vẫn diễn ra bình thường.

### 3.3. Chọn và liên kết repo (Manager)

1. `GET /api/workspaces/{id}/github/repos` trả danh sách: `[{ "owner", "name", "isPrivate", "defaultBranch" }]`.
2. `POST /api/workspaces/{id}/github/link` với `{ "owner", "name" }`.
3. Lỗi thường gặp: `GITHUB_NOT_CONNECTED` (chưa kết nối hoặc token hết hạn), `GITHUB_APP_NOT_INSTALLED` (mở `details.installUrl`), `GITHUB_REPO_NOT_ACCESSIBLE`.

**Token GitHub hết hạn sau khoảng 8 giờ nhưng backend tự làm mới** bằng refresh token (hiệu lực khoảng 6 tháng), nên người dùng hiếm khi phải kết nối lại. Chỉ khi refresh token hết hạn hoặc bị thu hồi thì các lời gọi cần GitHub trả `GITHUB_NOT_CONNECTED`; lúc đó FE nên hiện nút "Kết nối lại GitHub" (mục 3.2) thay vì báo lỗi chung.

---

## 4. Chạy backend ở máy local và test GitHub

Mục này đi từ máy trống tới lúc test được GitHub. Làm theo thứ tự. Mỗi bước có cách kiểm tra "đã đúng chưa".

### 4.0. Tổng quan: cần gì cho từng mức

| Mức | Test được gì | Cần |
|---|---|---|
| **Mức 0** | Gọi API, đăng nhập bằng email, workspace, DEFINE, LOCK (không AI thì không chạy được bước AI) | Mục 4.1 đến 4.5 |
| **Mức 1** | Đăng nhập bằng GitHub, kết nối GitHub, ngắt kết nối, lỗi `invalid_state` | Mức 0 + một GitHub OAuth App của riêng bạn (mục 4.6) |
| **Mức 2** | Liệt kê và liên kết repo, nhận feature (bot tạo nhánh), mời và gỡ thành viên khỏi repo, BUILD | Mức 1 + GitHub App `shipmate-bot-dev` và khóa riêng `.pem` + quyền vào repo thử (mục 4.7) |

Phần lớn việc của FE (đăng nhập, kết nối, màn hình cài đặt GitHub) **chỉ cần Mức 1**. Mức 2 chỉ cần với người làm màn hình liên kết repo, nhận feature và BUILD.

### 4.1. Cài công cụ

| Công cụ | Ghi chú |
|---|---|
| .NET 8 SDK trở lên | Kiểm tra: `dotnet --version` |
| PostgreSQL | Chạy ở `localhost:5432`. Backend dùng PostgreSQL (README còn ghi InMemory là đã lỗi thời) |
| Công cụ `dotnet-ef` | `dotnet tool install --global dotnet-ef`. Kiểm tra: `dotnet ef --version` |
| `git`, trình duyệt Chrome hoặc Edge | |

### 4.2. Tạo database và bảng

Backend **không tự tạo bảng**. Làm hai việc:

1. Tạo một database rỗng tên `shipmate` trong PostgreSQL (bằng pgAdmin, DBeaver hoặc `createdb`).
2. Tạo bảng bằng migration, chạy ở thư mục gốc của repo:

```bash
dotnet ef database update --project ShipMate.Infrastructure --startup-project ShipMate.API
```

Kiểm tra: chạy lệnh dưới, kết quả phải liệt kê các migration (cuối danh sách là `DriftThresholdConcurrencyToken`):

```bash
dotnet ef migrations list --project ShipMate.Infrastructure --startup-project ShipMate.API
```

Lỗi hay gặp: "password authentication failed" nghĩa là sai mật khẩu trong chuỗi kết nối ở mục 4.3.

### 4.3. Tạo file cấu hình

Repo có file mẫu `ShipMate.API/appsettings.Development.example.json` (không chứa bí mật). Sao chép thành `ShipMate.API/appsettings.Development.json`. File này **bị gitignore**, không bao giờ commit.

```bash
cp ShipMate.API/appsettings.Development.example.json ShipMate.API/appsettings.Development.json
```

Điền các giá trị:

| Khóa | Cách có giá trị | Bắt buộc |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | Đổi `Password=` thành mật khẩu PostgreSQL của bạn | Có |
| `Jwt:Secret` | Tự tạo: `openssl rand -base64 48` (chuỗi từ 32 ký tự trở lên). **Mỗi máy tự tạo, không cần dùng chung** | Có |
| `Encryption:Key` | Tự tạo: `openssl rand -base64 32` (đúng 32 byte, dạng base64). **Mỗi máy tự tạo.** Dùng để mã hóa token GitHub lưu trong DB của bạn | Có |
| `Resend:ApiKey` | Khóa gửi email (mã xác minh đăng ký, đặt lại mật khẩu). Có thể để trống nếu bạn đăng nhập bằng GitHub (xem 4.5) | Chỉ khi đăng ký bằng email |
| `Gemini:ApiKey` | Khóa AI. Lấy miễn phí ở Google AI Studio. Thiếu khóa thì các bước AI của DEFINE, LOCK, BUILD báo lỗi | Chỉ khi test bước AI |
| `GitHub:ClientId`, `GitHub:ClientSecret` | Mục 4.6 | Mức 1 trở lên |
| `GitHubApp:AppId`, `Slug`, `PrivateKeyPath` | Mục 4.7 | Mức 2 |

Nếu không có `openssl` (Windows), dùng PowerShell: `[Convert]::ToBase64String([byte[]](1..32 | ForEach-Object { Get-Random -Maximum 256 }))` cho `Encryption:Key`.

`App:FrontendBaseUrl` mặc định là `http://localhost:3000`. Nếu FE của bạn chạy cổng khác, đổi giá trị này. CORS và đích chuyển hướng sau khi đăng nhập GitHub đều dùng giá trị đó.

**Tuyệt đối không dán khóa, secret hay file `.pem` vào chat chung, issue, commit hoặc ảnh chụp màn hình.**

### 4.4. Tin chứng chỉ HTTPS của .NET

GitHub gọi về `https://localhost:7138/...` và cookie chống giả mạo có cờ `Secure`, nên backend **phải chạy HTTPS** và trình duyệt phải tin chứng chỉ. Làm một lần mỗi máy:

```bash
dotnet dev-certs https --trust
```

Windows hiện hộp thoại xác nhận, bấm Yes. Sau đó **đóng hẳn rồi mở lại trình duyệt**. Lệnh chỉ thêm chứng chỉ cho `localhost` của máy bạn, không ảnh hưởng gì khác.

### 4.5. Chạy backend và tạo tài khoản đầu tiên

```bash
dotnet run --project ShipMate.API --launch-profile https
```

Kiểm tra: mở `https://localhost:7138/swagger` thấy Swagger, **không có cảnh báo chứng chỉ**. Profile `http` mặc định (cổng 5140) **không dùng được cho GitHub**: đăng nhập sẽ lỗi `invalid_state`.

DB mới thì rỗng, bạn cần một tài khoản:

- **Cách nhanh nhất (cần Mức 1): đăng nhập bằng GitHub.** Tài khoản được tạo và đã xác minh email ngay, **không cần Resend**.
- **Đăng ký bằng email:** cần `Resend:ApiKey` thật và một địa chỉ email thật, vì mã xác minh được gửi qua email. Không có khóa thì bước đăng ký lỗi.

### 4.6. Mức 1: GitHub OAuth App của riêng bạn

Mỗi người tự tạo để **không phải dùng chung `ClientSecret`**. Mất khoảng 2 phút.

1. GitHub, **Settings**, **Developer settings**, **OAuth Apps**, **New OAuth App**.
2. Điền:
   - Application name: tùy ý (ví dụ `ShipMate local - <tên bạn>`).
   - Homepage URL: `http://localhost:3000`.
   - **Authorization callback URL: đúng `https://localhost:7138/api/auth/github/callback`** (sai một ký tự là callback hỏng).
3. Bấm **Register application**. Sao chép **Client ID**. Bấm **Generate a new client secret** và sao chép secret ngay (chỉ hiện một lần).
4. Dán vào `appsettings.Development.json`:
   - `GitHub:ClientId` là Client ID.
   - `GitHub:ClientSecret` là secret vừa tạo.
5. Khởi động lại backend.

Các quyền (`read:user user:email repo`) do backend tự đặt trong link, bạn không phải chọn.

**Kiểm tra Mức 1** (Swagger hoặc FE):
1. `GET https://localhost:7138/api/auth/github/login-url` trả `{ "url": "https://github.com/login/oauth/authorize?..." }`, và phản hồi đặt cookie `githubOAuthState`.
2. Mở `url` trong **cùng trình duyệt**, bấm **Authorize**.
3. Trình duyệt chuyển tới `http://localhost:3000/oauth-callback?handoff=...`. Trang này lỗi kết nối nếu FE chưa chạy, nhưng **thanh địa chỉ có `handoff=` nghĩa là backend đã làm đúng**.
4. Gọi `POST /api/auth/github/exchange` với `{ "handoffCode": "<giá trị handoff>" }`, phải nhận `accessToken`.

Giới hạn của OAuth App riêng: repo của **tổ chức** chỉ hiện trong danh sách repo nếu **chủ tổ chức duyệt** OAuth App của bạn. Với org thử `shipmate-test-org` việc này do Bảo làm, nên người cần Mức 2 hãy dùng mục 4.7.

### 4.7. Mức 2: liên kết repo, nhận feature, BUILD

Phần này cần hai thứ không tự tạo được, vì gắn với GitHub App `shipmate-bot-dev` và org thử:

1. **GitHub App `shipmate-bot-dev`**: xin Bảo `AppId` và `Slug`, cùng **file khóa riêng `.pem`** (qua kênh riêng, ví dụ gửi file trực tiếp). Lưu file ở ngoài repo, rồi đặt `GitHubApp:PrivateKeyPath` bằng đường dẫn đầy đủ tới file đó (ví dụ `C:/keys/shipmate-bot-dev.pem`). Nếu khóa bị lộ, nhờ Bảo thu hồi và tạo khóa mới.
2. **Quyền vào repo thử `shipmate-test-org/demo-app`** (đã cài sẵn App): nhờ Bảo thêm tài khoản GitHub của bạn vào org hoặc làm cộng tác viên của repo, và đảm bảo org cho phép OAuth App bạn đang dùng truy cập. Nếu việc duyệt OAuth App riêng của bạn là bất tiện, hãy xin Bảo dùng chung OAuth App đã được duyệt cho org thử (`ClientId` và `ClientSecret` qua kênh riêng).

**Kiểm tra Mức 2:**
1. Đăng nhập tài khoản ShipMate là **Manager** của một workspace, đã kết nối GitHub.
2. `GET /api/workspaces/{id}/github/repos` có `demo-app`.
3. `POST /api/workspaces/{id}/github/link` với `{ "owner": "shipmate-test-org", "name": "demo-app" }` trả 200.
4. `GET /api/workspaces/{id}/setup` có `repoLinked: true`, `appInstalled: true`, `mergeGateEnabled: true`.

Nếu bạn không muốn tự cấu hình Mức 2, cách khác là để **Bảo chạy backend** và FE nối vào địa chỉ đó (cần Bảo cấu hình CORS cho FE của bạn; nói riêng với Bảo).

### 4.8. Gọi từ Frontend

- Mọi lời gọi tới backend dùng `https://localhost:7138`, kèm `credentials: 'include'`.
- Trình duyệt phải là **một** trình duyệt từ lúc FE lấy link (`login-url` hoặc `connect-url`) tới lúc người dùng bấm Authorize. Cookie `githubOAuthState` lưu ở trình duyệt đã gọi; Authorize ở trình duyệt khác thì callback trả `invalid_state`.
- Cookie `githubOAuthState` sống 10 phút. Để trang chờ lâu hơn thì phải lấy link mới.
- Trình duyệt nhúng, cửa sổ ẩn danh hoặc trình duyệt khác với nơi gọi API đều có phiên đăng nhập GitHub riêng; hãy chắc người dùng đang đăng nhập đúng tài khoản GitHub.

### 4.9. Dữ liệu thử trên GitHub (Mức 2)

| Thứ | Giá trị |
|---|---|
| Org và repo thử | `shipmate-test-org/demo-app` |
| Bot | GitHub App `shipmate-bot-dev` (đã cài lên `demo-app`) |
| Tài khoản GitHub thử | Bảo cấp; mỗi tài khoản GitHub chỉ gắn được với **một** người dùng ShipMate |

Tài khoản mẫu cho DEFINE và LOCK nằm trong `define-demo-data.md` (file **không** commit, xin Bảo). Email của tài khoản mẫu là địa chỉ giả (`example.com`) nên email thông báo thật sự không gửi được; backend ghi cảnh báo rồi đi tiếp, không lỗi.

### 4.10. Lỗi thường gặp

| Hiện tượng | Nguyên nhân | Cách xử lý |
|---|---|---|
| Backend không khởi động, báo `Jwt:Secret is not configured` hoặc `Encryption:Key is not configured` | Thiếu khóa trong `appsettings.Development.json` | Điền ở mục 4.3 |
| Backend báo lỗi base64 khi khởi động | `Encryption:Key` không phải base64 hợp lệ | Tạo lại bằng `openssl rand -base64 32` |
| Lỗi kết nối PostgreSQL, hoặc "relation ... does not exist" | Chưa chạy PostgreSQL, sai mật khẩu, hoặc chưa chạy migration | Mục 4.2 |
| `ERR_CERT_AUTHORITY_INVALID` khi mở `https://localhost:7138` | Chưa tin chứng chỉ dev | Mục 4.4, mở lại trình duyệt |
| Chuyển về `/login?error=invalid_state` | Backend chạy HTTP; hoặc Authorize ở trình duyệt khác; hoặc để trang quá 10 phút | Chạy profile `https`, dùng một trình duyệt, lấy link mới |
| GitHub báo "redirect_uri is not associated with this application" | Callback URL của OAuth App không khớp | Đặt đúng `https://localhost:7138/api/auth/github/callback` |
| `/settings?error=GITHUB_ACCOUNT_ALREADY_LINKED` | Tài khoản GitHub đang đăng nhập đã gắn với người dùng ShipMate khác | Đăng nhập GitHub bằng tài khoản khác (đăng xuất GitHub trước) |
| Trình duyệt dẫn tới `http://localhost:3000/...` mà trang lỗi kết nối | FE chưa chạy ở cổng 3000 | Chạy FE ở 3000 hoặc đổi `App:FrontendBaseUrl` |
| Gọi API từ FE báo lỗi CORS | FE không chạy ở `http://localhost:3000`, hoặc thiếu `credentials: 'include'` | Dùng đúng origin; thêm `credentials` |
| `GET /github/repos` thiếu repo của org | Org chưa duyệt OAuth App của bạn | Nhờ chủ org duyệt, hoặc dùng OAuth App đã duyệt (mục 4.7) |
| `GITHUB_NOT_CONNECTED` dù đã kết nối từ trước | Refresh token hết hạn hoặc bị thu hồi (hoặc GitHub App bị gỡ quyền của người dùng) | Gọi lại `connect-url` (mục 3.2) |
| `GITHUB_APP_NOT_INSTALLED` | Chưa cài App lên repo | Mở `details.installUrl`, chọn repo, cài |
| Lỗi 500 khi liên kết repo và log nhắc `GitHubApp:AppId` hoặc `PrivateKeyPath` | Chưa cấu hình GitHub App cho Mức 2 | Mục 4.7 |
| Đăng ký bằng email lỗi | Thiếu hoặc sai `Resend:ApiKey` | Dùng đăng nhập GitHub, hoặc cấu hình Resend |
| Chấp nhận lời mời bị `GITHUB_REQUIRED_TO_JOIN` | Người dùng chưa kết nối GitHub | Cho họ kết nối (mục 3.2) rồi chấp nhận lại |

---

## 5. Danh sách việc FE nên kiểm tra sau khi đọc file này

- [ ] Màn hình chấp nhận lời mời xử lý `GITHUB_REQUIRED_TO_JOIN` bằng cách dẫn tới kết nối GitHub rồi quay lại.
- [ ] Hiển thị `repoInvite` sau khi chấp nhận (đặc biệt `sent` và `failed`).
- [ ] Nút xóa thành viên xử lý `502 GITHUB_REVOKE_FAILED`: cho thử lại, và `?force=true` kèm cảnh báo.
- [ ] Bỏ liên kết hoặc đổi repo xử lý `502 GITHUB_REVOKE_FAILED`.
- [ ] Ngắt kết nối GitHub xử lý `GITHUB_DISCONNECT_BLOCKED_BY_LINKED_WORKSPACES` bằng lời giải thích.
- [ ] Mọi chỗ gặp `GITHUB_NOT_CONNECTED` có nút kết nối lại GitHub.
- [ ] Không dùng `title` để phân loại lỗi (đã đổi theo mã HTTP); chỉ dùng `errorCode`.
