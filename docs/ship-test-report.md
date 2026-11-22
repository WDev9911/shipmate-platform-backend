# Báo cáo kiểm thử: luồng SHIP

- **Ngày:** 2026-11-22
- **Unit test:** `dotnet test ShipMate.Tests` cho **1027 / 1027 đạt** (thêm 178 so với 849 trước SHIP): `ShipServiceTests` 59, `ProductionUrlCheckerTests` 26, `PublicAddressTests` 35, `ProductionUrlPolicyTests` 17, `ScopeDisciplinePolicyTests` 15, `ShipOwnershipTests` 12, `StartShipAttemptRequestValidatorTests` 5, `ShipAttemptLimitPolicyTests` 4, và 5 test mới ở `NextBestActionServiceTests`.
- **Chạy thật:** API local (profile `https`) trên PostgreSQL 16.5, DB mới tạo bằng migration. Không gọi Gemini hay GitHub. Có gọi ra internet tới `example.com` và `badssl.com` (trang chuyên để thử SSL) qua bộ kiểm URL thật.
- **Dữ liệu thử:** tài khoản và workspace tạo qua API. Phần cần Gemini và GitHub (snapshot, feature, submission đã duyệt, checklist đã xong) chèn bằng SQL. Script thử nằm ngoài repo.
- Tài liệu luồng: `docs/flows/05-ship.md`.

## 1. Migration

| Kiểm | Kết quả |
|---|---|
| `AddShip` tạo `ship_attempts`, `ship_cards`, cột `workspaces.ship_ownership_token` | Đạt |
| Chỉ mục `ship_cards`: unique `(workspace_id, snapshot_version)`, unique `public_slug`, unique `ship_attempt_id`; FK tới `ship_attempts` (`Restrict`) và `workspaces` (`Cascade`) | Đạt |
| Down về `AddVerificationChecklists`: hai bảng và cột biến mất | Đạt |
| Up lại khi đã có 3 workspace: cả 3 được cấp token, đúng dạng `shipmate-` + 32 hex, không trùng nhau | Đạt |

## 2. Qua HTTP, trước và sau Lock (33 / 33 đạt)

| Nhóm | Kiểm | Kết quả |
|---|---|---|
| Trước Lock | `readiness`: `version: null`, chặn bởi `SHIP_NOT_LOCKED`; `POST attempts`: `409 SHIP_NOT_LOCKED` | Đạt |
| Token | Manager lấy được token (đúng giá trị trong DB) và `path`; Developer: `403 WORKSPACE_PERMISSION_DENIED` | Đạt |
| Quyền | Không đăng nhập: `401`; người ngoài workspace: `404`; Developer ship: `403` | Đạt |
| Đầu vào | `snapshotVersion: 0`, `url: ""`: `400 VALIDATION_FAILED` cho cả hai trường; version 2 không tồn tại: `404` | Đạt |
| Thẻ công khai | Slug không tồn tại, không đăng nhập: `404 NOT_FOUND` | Đạt |
| Feature chưa duyệt | Một feature mới ở `in_scope`: `readiness` nêu đúng feature đó (2 khóa, 1 duyệt); `POST attempts`: `409 SHIP_FEATURES_NOT_APPROVED` kèm feature | Đạt |
| Sẵn sàng | Duyệt nốt: `canShip: true`, không còn chặn gì | Đạt |
| NBA | Manager: `ShipVersion` cho version 1; Developer: `NoAction` | Đạt |
| URL sai dạng | `http://`, `localhost`, cổng 8443, có user/password: `400 SHIP_URL_INVALID` đúng `reason` từng ca | Đạt |
| SSRF | `127.0.0.1`, `169.254.169.254` (metadata cloud), `[::1]`, `10.0.0.8`, `192.168.1.1`: kết quả `ChecksFailed`, `SHIP_URL_NOT_PUBLIC`, không có thẻ | Đạt |
| Website thật | `https://example.com/#top`: URL chuẩn hóa còn `https://example.com/`, ping 200, SSL hợp lệ kèm ngày hết hạn, sở hữu trượt (`FILE_NOT_FOUND`): `SHIP_OWNERSHIP_UNVERIFIED` | Đạt |
| SSL thật | `expired.badssl.com`: `CERTIFICATE_EXPIRED`; `wrong.host.badssl.com`: `NAME_MISMATCH`; `self-signed.badssl.com`: `UNTRUSTED` | Đạt |
| Lịch sử | Developer thấy đủ 9 lần thử, mới nhất trước | Đạt |
| Kiểm lại | `recheck` lần thử `example.com`: một lần thử mới, cùng URL | Đạt |
| Không cấp nhầm | Sau mọi lần trên: 0 thẻ trong DB | Đạt |
| Giới hạn | Lần thử thứ 11 trong giờ: `429 SHIP_RATE_LIMITED` kèm `retryAfter` | Đạt |

## 3. Cấp thẻ trên PostgreSQL thật (22 / 22 đạt)

Không có website công khai nào phục vụ token của workspace thử, nên nhánh cấp thẻ được chạy bằng một chương trình nhỏ ngoài repo. Chương trình dựng đúng DI của app (`AddApplication`, `AddInfrastructure`), dùng `ShipService`, EF Core, repository và chuỗi audit thật, **chỉ thay bộ kiểm URL bằng bản luôn đạt** (chờ 0,8 giây để hai request đi cùng nhau). Trước đó ghi vào audit một `SCOPE_CHANGE` (RemoveAndReplace), một `SCOPE_BYPASS` và một `GOVERNANCE_DILUTION`; deadline là hôm qua. Phần còn lại kiểm qua HTTP.

| Nhóm | Kiểm | Kết quả |
|---|---|---|
| Đua tranh | Hai request ship cùng version cùng lúc, mỗi cái một DbContext: đúng một thẻ, request kia `SHIP_ALREADY_SHIPPED` | Đạt |
| Đua tranh | DB còn đúng 1 thẻ và 1 lần thử `shipped`; request thua không để lại dòng nào | Đạt |
| Chuỗi audit | Thứ tự `1 SCOPE_CHANGE, 2 SCOPE_BYPASS, 3 GOVERNANCE_DILUTION, 4 SHIP_CARD_MINTED`; điểm neo là 3 (bản ghi của thẻ nằm sau điểm neo) | Đạt |
| Điểm | 100 − (3 + 5 + 10 + 10 + 1) = **71** | Đạt |
| Time-to-Ship | Khoảng 3 ngày kể từ lần Lock đầu | Đạt |
| Đọc lại | Developer thấy thẻ, URL đã chuẩn hóa, `isPublic: false`; 7 thành phần đọc lại đúng từ cột jsonb (`remove_and_replace.outOf = 1`, `deadline_overrun.count = 1`) | Đạt |
| Xác minh | Thẻ mới: `valid: true`, điểm neo 3 | Đạt |
| Sau khi ship | `readiness`: `SHIP_ALREADY_SHIPPED` kèm `shipCardId`; ship lại: `409 SHIP_ALREADY_SHIPPED`; NBA của Manager: `NoAction` | Đạt |
| Công khai | Thẻ riêng tư: `404` ở địa chỉ công khai; Developer bật công khai: `403`; Manager bật: `200` | Đạt |
| Công khai | Không đăng nhập: đọc được thẻ, **không** có `id`, `shippedByUserId`, `publicSlug`; `verify` chỉ trả `valid`, `reason`, `anchorSequence`, `anchorHash`, `checkedAt` | Đạt |
| Sửa tay DB | Nâng `ship_cards.score` lên 95: `CARD_RECORD_MISMATCH` | Đạt |
| Sửa tay DB | Viết lại payload bản ghi Override (sequence 2): `AUDIT_CHAIN_BROKEN`; `readiness` báo `auditChainBrokenAt: 2` | Đạt |
| Khôi phục | Trả payload cũ: `valid: true` lại | Đạt |
| Riêng tư lại | Tắt công khai: địa chỉ công khai `404` | Đạt |

## 4. Log của server

- Không request nào trả `500`.
- Mọi lỗi nghiệp vụ được `GlobalExceptionHandler` ghi ở mức Information. Middleware xử lý lỗi của ASP.NET vẫn ghi thêm một dòng `fail:` có stack trace cho mỗi lỗi nghiệp vụ. Điều này có từ trước ở mọi luồng (ví dụ email không gửi được ở Auth), không riêng SHIP.

## 5. Chưa chạy thật

- Cấp thẻ qua HTTP với một website HTTPS công khai thật phục vụ token (cần một trang của nhóm, ví dụ GitHub Pages hay Vercel, có file `/.well-known/shipmate.txt`). Bộ kiểm URL thật và nhánh cấp thẻ thật đã chạy riêng ở mục 2 và 3.
- Sự kiện SignalR `ShipCardMinted` với frontend (đã có unit test là sự kiện được gửi).
- Workspace bị Governance Suspended hoặc đã lưu trữ qua HTTP (chỉ có unit test).
