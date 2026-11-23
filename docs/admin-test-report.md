# Báo cáo kiểm thử module Admin

Kiểm thử cho giai đoạn A (tài khoản) và B (cấu hình nền tảng) của [kế hoạch Admin](flows/06-admin.md), trên nhánh `feature/admin`. Chưa gồm giai đoạn C (nhật ký AI) và E (giám sát), vì chưa làm.

## 1. Cách kiểm

| Lớp | Cách làm | Ghi chú |
|---|---|---|
| Unit test | `dotnet test`, xUnit và NSubstitute | **1005 ca đạt, 0 lỗi** (trước giai đoạn A có 849) |
| HTTP thật | API chạy ở `http://localhost:5140` với PostgreSQL dev, gọi bằng script Node | Script **không nằm trong repo** (xem mục 6) |

**Điều kiện chạy HTTP:**
- Đã áp 2 migration lên DB dev: `ExtendAdminActionLogs` và `AddPlatformSettings`.
- Admin thử tạo bằng `AdminSeed` (tham số dòng lệnh). Developer thử đăng ký qua API rồi xác minh email bằng SQL.
- `Resend:ApiKey` để rỗng, nên không có email nào gửi ra ngoài. Việc khóa tài khoản luôn gặp lỗi gửi mail và vẫn thành công, nên cũng kiểm luôn điều đó.
- Không gọi GitHub hay Gemini.
- Tài khoản thử đều có dạng `*-http-check@shipmate.test`. Sau mỗi lần chạy, toàn bộ tài khoản, workspace, nhật ký thử được xóa và dòng `platform_settings` được trả về giá trị seed. Các tài khoản có sẵn trong DB không bị đụng tới.

## 2. Giai đoạn A: kết quả HTTP thật

Lần chạy 2026-11-22: 28 ca ở phần 1, 6 ca ở phần 2, **cả 34 đạt**. Lần chạy bù 2026-11-23 (đi cùng phần B) thêm các ca ở mục 2.4.

### 2.1. Quyền và danh sách user
| Ca | Kết quả |
|---|---|
| Không token gọi `/api/admin/*` | 401 |
| Developer gọi `/api/admin/*` | 403 |
| Admin gọi `GET /api/admin/users` | 200 |
| `page=0`, `pageSize=0`, `pageSize=1000000`, `role=1`, `status=9`, `search` dài 101 ký tự | 400 cả sáu |
| Lọc `role=Administrator`, tìm theo tên không phân biệt hoa thường | đúng |
| Tìm `%_%` | 0 kết quả (ký tự đặc biệt được coi là chữ) |
| `PATCH /api/admin/users/{id}/role` | 404 (API đã xóa) |
| Admin gọi `POST /api/workspaces` | 403 `ADMIN_CANNOT_USE_WORKSPACES` |

### 2.2. Khóa tài khoản
| Ca | Kết quả |
|---|---|
| Admin khóa chính mình | 400 `SELF_ACTION_NOT_ALLOWED` |
| Admin khóa dev2 khi gửi email lỗi | 200, khóa vẫn có hiệu lực |
| Kết nối SignalR đang mở của dev2 | bị cắt ngay |
| Access token cũ của dev2 | 401 |
| Đăng nhập dev2 sai mật khẩu | 401 `INVALID_CREDENTIALS` |
| Đăng nhập dev2 đúng mật khẩu | 403 `ACCOUNT_LOCKED` |
| Mở kết nối hub mới bằng token cũ | 401 |
| Refresh bằng token đã bị thu hồi | 400 |
| Quên mật khẩu của tài khoản bị khóa | 200 như mọi tài khoản, không có mã nào được lưu trong DB |
| Manager mời tài khoản bị khóa | 404 như email không tồn tại |

### 2.3. Quy tắc trạng thái và nhật ký
| Ca | Kết quả |
|---|---|
| Khóa tài khoản đã khóa | 400 `NO_CHANGE` |
| Mở khóa rồi đăng nhập lại | 200 |
| Xóa (`Removed`) rồi đổi trạng thái | 409 `ACCOUNT_REMOVED_FINAL`, cả với `Active` lẫn `Removed` |
| `GET /api/admin/action-logs` | 3 dòng cho dev2, mới nhất trước, lọc và phân trang đúng |
| `GET /api/admin/users/{id}/activity` | vẫn hoạt động, có 3 thao tác |

### 2.4. Chạy bù ngày 2026-11-23
| Ca | Kết quả |
|---|---|
| Tìm `dev_3` và `dev_` | chỉ trả tài khoản có dấu gạch dưới thật, không trả `dev1` hay `dev2` |
| `accountLocked` trong danh sách thành viên | `false` với mọi người, sau khi khóa dev2 thì dev2 là `true` |
| Admin đổi trạng thái của Admin khác | 403 `ADMIN_ACCOUNT_PROTECTED`, kể cả khi đổi sang đúng trạng thái đang có; DB không đổi, không có dòng nhật ký |

### 2.5. Admin và workspace (dùng dữ liệu "sửa tay")
Tôi nâng một Manager của một workspace lên Admin bằng SQL để mô phỏng tài khoản bị sửa tay:
- Người đó mở workspace, xem danh sách thành viên: 404.
- Token cũ mang claim Developer cũng bị 404, vì guard đọc role từ DB chứ không từ claim.
- `GET /api/workspaces` của Admin trả danh sách (1 mục). Danh sách chưa bị chặn theo thiết kế (plan, mục 13.9); mở workspace thì bị chặn nên không lộ nội dung.

## 3. Giai đoạn B: kết quả HTTP thật (2026-11-23)

Lần chạy đầu 56 ca: **53 đạt, 3 báo fail**. Cả 3 được giải thích ở mục 4. Chạy lại riêng các điểm đó: 4 ca đạt.

| Nhóm | Ca | Kết quả |
|---|---|---|
| Quyền | Không token, Developer gọi `GET` và `PUT /settings` | 401, 403, 403 |
| Đọc | `GET /settings` | 200, đúng giá trị seed (10, 0.75, 4 mẫu, `revision` 0) và các giới hạn chỉ-đọc |
| Kiểm đầu vào | Ngân sách 4 và 51; ngưỡng 0.69, 0.86, 0.755; không có mẫu; 31 mẫu; mẫu dài 2 và 51 ký tự; mẫu trùng khác hoa thường; mẫu `null`; `revision` âm | 400 cả 11 ca; không có gì được lưu |
| `revision` | `revision` sai | 409 `SETTINGS_CHANGED_BY_OTHER` |
| `revision` | Lưu giá trị y hệt | 400 `NO_CHANGE`, `revision` không đổi |
| Lưu thật | Đổi cả 3 trường | 200, `revision` tăng, mẫu được cắt khoảng trắng và đổi chữ thường, có `updatedAt` và `updatedByUserId` |
| Nhật ký | `GET /action-logs?action=SettingsChanged` | 3 dòng cho 3 trường đã đổi, không có tài khoản đích, đúng giá trị cũ và mới |
| Nhật ký | Đổi riêng ngân sách | thêm đúng 1 dòng |
| **Hai Admin lưu cùng lúc** | 4 lượt, hai `PUT` song song cùng `revision` | **lần nào cũng đúng 1 bên 200, 1 bên 409** |
| Thử mẫu | Dùng mẫu đã lưu; dùng mẫu tự cho (không lưu); kèm file (comment thì bỏ qua AI, logic thì không) | đúng cả ba |
| Thử mẫu | Không có message; 101 file; patch tổng vượt 60.000 ký tự; mẫu 2 ký tự; Developer gọi | 400, 400, 400, 400, 403 |
| Thử mẫu | Số dòng nhật ký sau các lần thử | không tăng (endpoint không ghi gì) |
| Workspace mới | Tạo workspace khi cấu hình là (15, 0.80), rồi đổi thành (30, 0.70) và tạo thêm một workspace | workspace thứ nhất `15 | 0.80`, workspace thứ hai `30 | 0.70`, workspace thứ nhất không bị đổi |
| Workspace cũ | 21 workspace có sẵn | vẫn đều là 10 |

## 4. Ba ca báo fail ở lần chạy đầu của giai đoạn B

| Ca | Nguyên nhân | Xử lý |
|---|---|---|
| `PUT` không có body trả 415 | **Lỗi script:** không gửi `Content-Type`. ASP.NET Core trả 415, đúng với mọi API của dự án | Không sửa code. Gửi kèm `Content-Type: application/json` thì thiếu body và JSON hỏng đều trả 400 |
| Nhật ký ghi `0.75 -> 0.8` thay vì `0.80` | **Lỗi thật, nhỏ:** giá trị mới đến từ JSON không giữ số 0 cuối, giá trị cũ từ cột `numeric(4,2)` thì có | Đã sửa: ghi ngưỡng bằng định dạng `0.00` cho cả hai phía, thêm test, chạy lại thật được `0.70 -> 0.80` |
| Dòng giá trị workspace đầu tiên | **Lỗi script:** tách dòng bằng `\n` làm dư ký tự `\r` của Windows. Giá trị trong DB là `15` và `0.80`, đúng | Không sửa code |

## 5. Vấn đề phát hiện khi kiểm

**Phép đếm "Admin cuối" không có tác dụng.** Bản đầu của bước A.4 đếm số Admin còn lại trước khi khóa một Admin. Thử thật bằng cách cho hai Admin khóa nhau cùng lúc: ở lượt thứ 2 cả hai cùng thành công và **không còn Admin nào**, và `LAST_ADMINISTRATOR` chưa bao giờ trả về. Lý do: người gọi luôn là Admin đang `Active` và được tính vào phép đếm, nên số còn lại luôn ≥ 1. Đã đổi sang luật đơn giản hơn: **Admin không đổi trạng thái của Admin khác** (403 `ADMIN_ACCOUNT_PROTECTED`), có unit test và đã kiểm HTTP (mục 2.4). Phương án dùng khóa DB để vẫn cho Admin khóa Admin được giữ ở mục 13.6 của plan.

## 6. Những gì chưa kiểm

**Chỉ có unit test, chưa chạy thật:**
- Đăng nhập GitHub với tài khoản bị khóa (cả 4 ca): cần GitHub thật.
- Refresh token và xác minh email với tài khoản bị khóa. Lần HTTP chỉ chạm nhánh token đã bị thu hồi, không chạm nhánh kiểm trạng thái tài khoản.
- Nội dung và việc gửi email báo khóa hoặc xóa tài khoản: đã tắt gửi mail.
- Seed với mật khẩu yếu, hoặc với email của một tài khoản Developer.
- BUILD dùng danh sách mẫu từ cấu hình: cần GitHub thật.
- Lock và Feature Challenge dùng ngân sách của workspace: cần có kết quả phân tích AI.

**Chưa kiểm ở đâu cả:**
- Chiều `Down` của hai migration.
- Hiệu năng: mỗi request nay có thêm một truy vấn theo khóa chính để kiểm trạng thái tài khoản; chưa đo.
- Giao diện frontend.

## 7. Về các script kiểm HTTP

Các script nằm ở thư mục tạm của phiên làm việc, không nằm trong repo (quyết định 2026-11-23: không đưa vào, theo YAGNI). Hệ quả: không có bộ kiểm hồi quy tự động cho API Admin, và muốn chạy lại thì phải viết lại theo mô tả ở các bảng trên. Báo cáo này là nơi lưu lại những gì đã chạy.

Cách chạy lại, nếu cần:
1. Áp migration: `dotnet ef database update --project ShipMate.Infrastructure --startup-project ShipMate.API`.
2. Chạy API với `--AdminSeed:Email=...`, `--AdminSeed:Password=...` và `--Resend:ApiKey=` (rỗng, để không gửi email thật).
3. Đăng ký các Developer thử qua `POST /api/auth/register` (trả 502 vì không gửi được email nhưng tài khoản vẫn được tạo), rồi đặt `is_email_verified = true` bằng SQL.
4. Sau khi kiểm xong: xóa các tài khoản thử và dữ liệu liên quan, và trả `platform_settings` về giá trị seed (10, 0.75, 4 mẫu, `revision` 0).
