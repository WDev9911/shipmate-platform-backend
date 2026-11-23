# Luồng 5 (Ship)

Tài liệu này mô tả **phần SHIP đã cài đặt**, cùng cách viết với `02-lock.md`. Nguyên văn spec 2.5 nằm ở tài liệu thiết kế của nhóm; ở đây ghi cách code hiện hành hiểu và làm, những chỗ khác spec (kèm lý do) và những chỗ còn phải chốt.

**Mục đích:** xác nhận version mới nhất của scope đã thật sự chạy trên production, tính hai chỉ số (Time-to-Ship, Scope Discipline Score) từ dữ liệu LOCK, BUILD, VERIFY đã ghi, và cấp một Ship Card mà người ngoài kiểm được là không bị sửa.

**Phân quyền.** Chỉ Manager kiểm URL, cấp thẻ và bật công khai. Mọi thành viên đang hoạt động xem được trạng thái sẵn sàng, các lần thử và các thẻ. Thẻ công khai thì ai cũng xem và xác minh được, không cần đăng nhập.

SHIP **không gọi AI**. Gọi mạng duy nhất là tới URL production do đội nhập, và luôn qua lớp chặn SSRF (mục 6).

## Mục lục

1. Tiến độ
2. Khác spec 2.5 (và lý do)
3. Dữ liệu dùng lại và dữ liệu mới
4. Trình tự một lần ship
5. Những gì chặn SHIP
6. Kiểm tra URL an toàn
7. Time-to-Ship
8. Scope Discipline Score
9. Ship Card và xác minh
10. API
11. File chính
12. Kiểm thử
13. Cần chốt với thầy Vũ
14. Giới hạn đã biết

## 1. Tiến độ (cập nhật 2026-11-22)

| Phần | Trạng thái |
|---|---|
| Domain: `ShipAttempt`, `ShipCard`, `ScopeDisciplinePolicy`, `ProductionUrlPolicy`, `ShipOwnership`, `ShipAttemptLimitPolicy` | Xong |
| DB: bảng `ship_attempts`, `ship_cards`, cột `workspaces.ship_ownership_token`, migration `AddShip` | Xong, đã chạy vào DB dev, đã thử Down rồi Up |
| Kiểm URL an toàn (`ProductionUrlChecker`, chặn SSRF) | Xong, đã chạy với website thật |
| Service, controller, mã lỗi, SignalR `ShipCardMinted` | Xong |
| NBA: hành động `ShipVersion` cho Manager | Xong |
| Unit test | 178 test mới, toàn bộ 1027/1027 đạt |
| Chạy thật qua HTTP và PostgreSQL | 55/55 đạt, xem `docs/ship-test-report.md` |
| Cấp thẻ qua HTTP với một website thật phục vụ token | **Chưa chạy**: cần một trang HTTPS công khai của nhóm. Nhánh cấp thẻ đã chạy thật trên PostgreSQL bằng service thật, chỉ thay bước kiểm URL bằng bản luôn đạt |

Code nằm trên nhánh `feature/ship`, tạo từ `main` (sau PR #18).

## 2. Khác spec 2.5 (và lý do)

| # | Spec 2.5 | Đã làm | Lý do |
|---|---|---|---|
| S1 | Bước 2 đọc `all_items_checked` | Gọi `IVerificationService.GetReadinessAsync`, chặn khi chưa xong, trả kèm `missingItems` | Spec cần "hiển thị rõ mục nào còn thiếu" (`04-verify.md` D7) |
| S2 | Governance Suspended khi OAuth token bị revoke | Cờ là `Workspace.GovernanceSuspendedAt`, bật khi **App ShipMate bị gỡ** khỏi repo | BUILD đã chọn điều kiện này: OAuth token chỉ dùng để đăng nhập, không dùng để giữ repo |
| S3 | Ping 200 và SSL hợp lệ là đủ | Thêm **chặn SSRF** và **bằng chứng sở hữu** URL | Không có thì (a) server ShipMate bị dùng để gọi vào mạng nội bộ, (b) `https://google.com` cũng ship được |
| S4 | "Chạy lại đúng bước Ping + SSL" | Mỗi lần chạy là **một dòng `ship_attempts` mới**; `recheck` tạo lần thử mới với cùng version và URL | Lịch sử giữ mọi lần kiểm, và giới hạn 10 lần/giờ đếm được từ chính bảng này |
| S5 | Ship nhiều lần, mỗi lần một thẻ | **Một thẻ cho mỗi version** (unique `workspace_id, snapshot_version`) | Bấm đúp hay hai Manager bấm cùng lúc không sinh thẻ trùng; không "cày" thẻ được |
| S6 | Công thức điểm chưa chốt | Lưu **từng thành phần**, **phiên bản công thức** và **điểm neo audit** trên thẻ; trọng số nằm một chỗ | Công thức sẽ đổi sau khi hỏi thầy Vũ; thẻ cũ vẫn tự giải thích được |
| S7 | "Ai cũng đối chiếu được hash" | Endpoint công khai chỉ trả **thẻ còn khớp hay không**, không lộ nội dung audit | Chuỗi audit là dữ liệu riêng của workspace |
| S8 | "Dev" thực hiện SHIP | **Chỉ Manager** | Thẻ đại diện cả đội và có thể công khai. Workspace một người thì Manager chính là dev |
| S9 | Ship Card "chia sẻ công khai" | Mặc định **riêng tư**; Manager bật công khai | Thẻ có tên sản phẩm và điểm của đội |
| S10 | Không nói về version cũ | **Chỉ ship version mới nhất.** Client vẫn gửi `snapshotVersion`; khác version mới nhất thì `SHIP_VERSION_NOT_LATEST` | Bản kế hoạch cho ship version cũ, nhưng khi đó "feature đã duyệt" và "drift tồn đọng" không còn rõ thuộc version nào. Scope đã bị Feature Challenge thay thì không còn là thứ đội cam kết. Gửi kèm version để không ship nhầm một version vừa đổi lúc trang đang mở |
| S11 | Không yêu cầu BUILD | **Mọi feature `Locked` của version phải có submission mới nhất `Approved`** (`SHIP_FEATURES_NOT_APPROVED`) | Không có luật này thì đội bỏ qua hẳn BUILD (không bao giờ submit) vẫn ship được và đạt **100 điểm**, vì không có gì để trừ. Feature `Backup` và `Replaced` không tính. Định nghĩa "đã duyệt" giống hệt VERIFY và NBA |
| S12 | Drift tồn đọng đếm theo `SnapshotVersionAtCheck` | Đếm theo **feature còn `Locked` trong version đang ship**, bất kể cảnh báo tạo ở version nào | Đếm theo `SnapshotVersionAtCheck` thì một Feature Challenge nhỏ (tạo version mới) làm mọi drift cũ biến khỏi điểm. Nay SHIP và VERIFY đếm giống nhau |
| S13 | Không có | Chuỗi audit đã bị sửa thì **không cấp thẻ** (`SHIP_AUDIT_CHAIN_BROKEN`) | Thẻ neo vào lịch sử; lịch sử đã hỏng thì không có gì để neo |
| S14 | Không có | **Kiểm lại mọi điều kiện ngay trước khi cấp thẻ**, đọc mới từ DB | Kiểm URL mất tới khoảng 30 giây; trong lúc đó có thể có version mới, một mục checklist bị bỏ tick, hoặc mất quyền repo |
| S15 | Không có | Bản ghi `SHIP_CARD_MINTED` chứa **mọi số liệu của thẻ** (kể cả bảng điểm thành phần) | Điểm neo chỉ chứng minh lịch sử trước thẻ không bị sửa. Ghi cả thẻ vào chuỗi thì sửa điểm trên thẻ trong DB cũng bị phát hiện |
| S16 | Không có | NBA thêm `ShipVersion` cho Manager khi version đã duyệt hết, checklist xong và chưa ship | Khép vòng Path to Ship: NBA dẫn tới bước cuối |

## 3. Dữ liệu dùng lại và dữ liệu mới

**Dùng lại**

| Cần | Ở đâu |
|---|---|
| Mốc Time-to-Ship | `Workspace.FirstLockedAt` (LOCK ghi một lần, không ghi đè) |
| Version mới nhất và feature của nó | `mvp_snapshots`, `snapshot_features` |
| Feature đã duyệt | `IBuildSubmissionRepository.GetLatestPerFeatureAsync` (submission mới nhất `Approved`) |
| Sẵn sàng VERIFY | `IVerificationService.GetReadinessAsync` |
| Chặn khi mất quyền repo | `Workspace.GovernanceSuspendedAt` |
| Đổi scope, override, hạ ngưỡng, push lén | `audit_trail`: `SCOPE_CHANGE` (kèm `action`), `SCOPE_BYPASS`, `GOVERNANCE_DILUTION`, `DIRECT_PUSH_DETECTED` |
| Drift tồn đọng | `IDriftAlertRepository.GetOpenByWorkspaceAsync`, lọc theo feature `Locked` |
| Deadline | `Workspace.LaunchDeadline` (có thể null) |

**Mới**

- `ship_attempts`: mỗi lần kiểm một dòng. `url` (đã chuẩn hóa), `status` (`checks_failed`, `shipped`), `failure_code` (bước trượt đầu tiên), `ping_status_code`, `ping_error`, `ssl_valid`, `ssl_error`, `ssl_expires_at`, `ownership_verified`, `ownership_error`, `checked_at`, `created_by_user_id`, `created_at`. Chỉ mục `(workspace_id, created_at)` cho lịch sử và giới hạn tần suất.
- `ship_cards`: unique `(workspace_id, snapshot_version)`, unique `public_slug`, unique `ship_attempt_id` (FK, `Restrict`). `product_name` (chụp tên workspace lúc ship), `url`, `shipped_at`, `shipped_by_user_id`, `time_to_ship_seconds`, `score`, `score_formula_version`, `score_breakdown` (jsonb), `verification_checklist_id`, `verification_completed_at`, `audit_anchor_sequence`, `audit_anchor_hash`, `is_public`, `public_slug`. Không có FK tới checklist hay user: thẻ giữ nguyên những gì nó đã ghi.
- `workspaces.ship_ownership_token`: `shipmate-` + 32 ký tự hex, sinh khi tạo workspace. Migration cấp token cho mọi workspace có sẵn.
- `audit_trail`: loại mới `SHIP_CARD_MINTED`.
- `IVerificationChecklistRepository.GetCurrentByVersionAsync` và `IWorkspaceRepository.GetCurrentAsync`: đọc mới, không theo dõi (no-tracking), để lần kiểm lại trước khi cấp thẻ không đọc bản cũ EF đang giữ (S14). `VerificationService.GetReadinessAsync` chuyển sang dùng hàm đọc mới này; hành vi với VERIFY và NBA không đổi.

## 4. Trình tự một lần ship

`POST .../ship/attempts` với `{ snapshotVersion, url }`, chỉ Manager:

1. Chuẩn hóa URL (`ProductionUrlPolicy`): chỉ `https`, cổng 443, không có user/password, không phải `localhost`; bỏ `#…`; host chữ thường. Sai thì `SHIP_URL_INVALID` (400, kèm `details.reason`).
2. Đọc trạng thái và kiểm mọi điều kiện ở mục 5. Trượt điều kiện nào thì trả lỗi của điều kiện đó; **chưa có request nào rời server**.
3. Giới hạn tần suất: tối đa 10 lần thử mỗi workspace trong 1 giờ (`SHIP_RATE_LIMITED`, 429, kèm `retryAfter`).
4. Kiểm URL (mục 6). Trượt bước nào thì lưu lần thử `ChecksFailed` kèm lỗi từng bước, trả `200` (đây là kết quả kiểm tra, không phải lỗi request).
5. Qua hết: **đọc lại và kiểm lại** mọi điều kiện ở mục 5 (S14). Rồi trong **một** lần lưu:
   - điểm neo là bản ghi audit cuối cùng (`Sequence`, `RecordHash`; workspace chưa có bản ghi nào thì neo ở đầu chuỗi: sequence 0, hash toàn số 0);
   - tính Time-to-Ship và điểm từ các bản ghi tới điểm neo (mục 7, 8);
   - tạo Ship Card, đặt lần thử là `Shipped`;
   - ghi `SHIP_CARD_MINTED` (nằm **sau** điểm neo).
6. Hai request cùng ship một version: index unique của `ship_cards` cho một bên thắng, bên thua nhận `SHIP_ALREADY_SHIPPED` và không để lại dòng nào. Đã kiểm trên PostgreSQL thật.
7. Gửi SignalR `ShipCardMinted` tới mọi thành viên.

`POST .../attempts/{id}/recheck`: lấy version và URL của lần thử cũ, chạy lại từ bước 1 thành một lần thử mới.

## 5. Những gì chặn SHIP

Kiểm theo thứ tự này. `GET .../ship/readiness` trả **mọi** điều kiện đang chặn cùng lúc, cũng theo thứ tự này, để frontend hiện hết trước khi bấm.

| # | Điều kiện | Mã lỗi |
|---|---|---|
| 1 | Workspace không bị lưu trữ | `SHIP_WORKSPACE_ARCHIVED` (409) |
| 2 | Đã Lock ít nhất một lần | `SHIP_NOT_LOCKED` (409) |
| 3 | `snapshotVersion` tồn tại và là version mới nhất (chỉ khi ship) | `NOT_FOUND` (404) / `SHIP_VERSION_NOT_LATEST` (409) |
| 4 | Version chưa có thẻ | `SHIP_ALREADY_SHIPPED` (409) |
| 5 | Không bị Governance Suspended | `SHIP_GOVERNANCE_SUSPENDED` (409) |
| 6 | Có ít nhất một feature `Locked`, và mọi feature `Locked` có submission mới nhất `Approved` | `SHIP_FEATURES_NOT_APPROVED` (409, kèm danh sách) |
| 7 | Checklist VERIFY của version có và đã xong | `SHIP_VERIFY_INCOMPLETE` (409, kèm `missingItems`) |
| 8 | Chuỗi audit còn nguyên | `SHIP_AUDIT_CHAIN_BROKEN` (409, kèm `brokenAtSequence`) |

Chặn cứng là 8 điều kiện trên cộng với URL hỏng. Mọi thứ ở mục 8 chỉ trừ điểm, không chặn (đúng spec, tránh "Failure to Ship").

## 6. Kiểm tra URL an toàn

Server ShipMate tự gọi một URL do người dùng nhập, nên đây là chỗ dễ bị lợi dụng nhất của SHIP. Code ở `ShipMate.Infrastructure/Services/Ship/`.

**Chặn SSRF**
- Mỗi bước (URL ban đầu và từng lần chuyển hướng) phải qua `ProductionUrlPolicy`: `https`, cổng 443, không user/password, không `localhost`.
- Phân giải DNS và từ chối nếu **bất kỳ** địa chỉ nào không công khai (`PublicAddress`): loopback, private (10/8, 172.16/12, 192.168/16), link-local 169.254/16 (gồm metadata của cloud), CGNAT 100.64/10, multicast, các dải dành riêng và tài liệu; IPv6 chỉ nhận global unicast 2000::/3, trừ 2001::/23 (gồm Teredo), 2001:db8::/32, và 6to4 2002::/16 bọc một IPv4 không công khai; IPv4 viết dạng IPv6 (`::ffff:127.0.0.1`) được quy về IPv4.
- Kiểm lại **lúc kết nối**: `SocketsHttpHandler.ConnectCallback` phân giải lại và chỉ kết nối tới đúng các địa chỉ vừa kiểm, nên tên miền đổi IP giữa chừng (DNS rebinding) cũng không dẫn vào trong được.
- Không tự theo chuyển hướng; tự theo tối đa 3 lần, mỗi lần kiểm lại như trên. Không dùng proxy, không cookie.
- Mỗi request tối đa 10 giây, cả lần kiểm tối đa 30 giây. Không đọc nội dung trang; file token chỉ đọc tối đa 1 KB. Không trả nội dung phản hồi về cho người dùng.

**Ping:** sau tối đa 3 chuyển hướng, mã cuối phải là `200`.

**SSL:** kiểm tra mặc định của .NET (chuỗi tin cậy, đúng tên miền, còn hạn). Lưu ngày hết hạn. Lỗi được phân loại: `CERTIFICATE_EXPIRED`, `NAME_MISMATCH`, `UNTRUSTED`, `NOT_PROVIDED`, `INVALID`.

**Quyền sở hữu (S3):** `https://<host đã nhập>/.well-known/shipmate.txt` phải có một dòng đúng bằng `ship_ownership_token` (bỏ khoảng trắng và BOM; nhiều dòng được, để một tên miền phục vụ nhiều workspace). Chỉ theo chuyển hướng **trong cùng host**: trang chuyển sang host khác thì không tính. Nhờ vậy một trang có lỗ hổng open redirect của người khác không thể "chứng minh" là của đội.

## 7. Time-to-Ship

`shipped_at − Workspace.FirstLockedAt`, lưu bằng giây. Không tính từ lúc tạo version đang ship (đúng spec, chống "reset đồng hồ" bằng một Feature Challenge nhỏ). Mọi lần ship sau dùng cùng mốc. `FirstLockedAt` null khi đã có snapshot là dữ liệu hỏng: báo lỗi, không đoán.

## 8. Scope Discipline Score

Tính trong chính sách thuần `ScopeDisciplinePolicy` (Domain): đầu vào là các con số đã đếm, đầu ra là điểm và từng thành phần. Đổi trọng số chỉ sửa một file và tăng `FormulaVersion`.

**Đếm.** Mọi thứ đọc từ `audit_trail` chỉ tính các bản ghi tới điểm neo:

| Thành phần (`key`) | Đếm |
|---|---|
| `scope_volatility` | Số `SCOPE_CHANGE` (mọi loại challenge được duyệt) |
| `scope_bypass` | Số `SCOPE_BYPASS` (`SOLO_OVERRIDE_LOGGED` chỉ là cờ, không đếm thêm) |
| `governance_dilution` | Số `GOVERNANCE_DILUTION` |
| `open_drift` | Cảnh báo `Unresolved` hoặc `Acknowledged` trên feature còn `Locked` trong version đang ship, bất kể cảnh báo tạo ở version nào (S12). Đây là trạng thái lúc cấp thẻ |
| `remove_and_replace` | `RemoveAndReplace / (RemoveAndReplace + AddDirectly)`, đọc từ trường `action` của `SCOPE_CHANGE`; `Backup` không tính; mẫu số 0 thì tỉ lệ 0 |
| `deadline_overrun` | Số ngày `shipped_at` sau ngày `LaunchDeadline` (tính theo ngày UTC; ship trong ngày deadline là đúng hạn); không có deadline thì 0 |
| `direct_push` | Số `DIRECT_PUSH_DETECTED` (ngoài spec) |

**Công thức nháp v0** (để code chạy được; phải chốt với thầy Vũ, mục 13): bắt đầu 100, trừ theo bảng, mỗi thành phần có trần, kết quả kẹp trong 0–100.

| Thành phần | Trừ | Trần |
|---|---|---|
| `scope_volatility` | 3 mỗi lần | 15 |
| `scope_bypass` | 5 mỗi lần | 25 |
| `governance_dilution` | 10 mỗi lần | 20 |
| `open_drift` | 5 mỗi cảnh báo | 15 |
| `remove_and_replace` | tỉ lệ × 10, làm tròn | 10 |
| `deadline_overrun` | 1 mỗi ngày | 15 |
| `direct_push` | 5 mỗi lần | 10 |

Trần giữ cho một loại lỗi không tự kéo điểm về 0, và giúp mỗi thành phần đọc được trên thẻ ("Override: −10/25"). `score_breakdown` lưu số đếm, điểm trừ, trần (và `outOf` cho tỉ lệ) của từng thành phần.

## 9. Ship Card và xác minh

Thẻ hiện: tên sản phẩm, version, URL, ngày ship, Time-to-Ship, điểm kèm 7 thành phần, phiên bản công thức, lúc checklist hoàn tất, điểm neo.

**Xác minh** (`GET .../cards/{id}/verify` cho thành viên, `GET /api/ship-cards/{slug}/verify` cho thẻ công khai) trả `valid` và lý do khi không hợp lệ:

| Kiểm | Không qua thì |
|---|---|
| Bản ghi `SHIP_CARD_MINTED` của thẻ còn trong chuỗi | `CARD_RECORD_MISSING` |
| Chuỗi nguyên vẹn **tới bản ghi của thẻ** (phần thêm sau thẻ không tính) | `AUDIT_CHAIN_BROKEN` |
| `RecordHash` ở `audit_anchor_sequence` đúng bằng `audit_anchor_hash` trên thẻ | `ANCHOR_MISMATCH` |
| Thẻ còn nói đúng những gì bản ghi của nó đã ghi (version, URL, điểm, công thức, Time-to-Ship, điểm neo, từng thành phần) | `CARD_RECORD_MISMATCH` |

Không trả nội dung audit nào. Nói đúng mức: điều này chứng minh **thẻ và phần lịch sử nó đã đếm không bị sửa sau khi cấp thẻ**. Nó không chứng minh lịch sử đầy đủ hay trung thực ngay từ đầu (ví dụ push lén mà GitHub không báo thì không có bản ghi).

Thẻ riêng tư trả 404 ở địa chỉ công khai, giống hệt thẻ không tồn tại. `public_slug` là 16 byte ngẫu nhiên (base64url), có ngay từ lúc cấp thẻ nhưng chỉ dùng được khi thẻ đang công khai.

## 10. API

Route gốc `api/workspaces/{workspaceId}/ship`. Hình dạng JSON và bảng mã lỗi đầy đủ ở `docs/frontend-guide.md` mục 2.12.

| Method | Route | Ai | Việc |
|---|---|---|---|
| GET | `readiness` | Thành viên | Mục 5, mọi điều kiện đang chặn |
| GET | `ownership-token` | Manager | Token và đường dẫn file |
| POST | `attempts` | Manager | Mục 4 |
| POST | `attempts/{attemptId}/recheck` | Manager | Chạy lại thành lần thử mới |
| GET | `attempts` | Thành viên | 50 lần thử gần nhất |
| GET | `cards` | Thành viên | Các thẻ, version mới nhất trước |
| GET | `cards/{cardId}` | Thành viên | Một thẻ |
| GET | `cards/{cardId}/verify` | Thành viên | Mục 9 |
| PUT | `cards/{cardId}/visibility` | Manager | `{ "isPublic": bool }` |
| GET | `/api/ship-cards/{slug}` | Ai cũng được | Thẻ công khai |
| GET | `/api/ship-cards/{slug}/verify` | Ai cũng được | Mục 9 |

NBA: `ShipVersion` (kèm `snapshotVersion`) cho Manager khi version mới nhất đã duyệt hết, checklist xong và chưa có thẻ; Developer nhận `NoAction`. SignalR: `ShipCardMinted` `{ cardId, version, score, timeToShipSeconds }`.

## 11. File chính

| Tầng | File |
|---|---|
| Domain | `Entities/ShipAttempt.cs`, `Entities/ShipCard.cs`, `Enums/ShipAttemptStatus.cs`, `Policies/ScopeDisciplinePolicy.cs`, `Policies/ProductionUrlPolicy.cs`, `Policies/ShipOwnership.cs`, `Policies/ShipAttemptLimitPolicy.cs`; sửa `Entities/Workspace.cs`, `Common/AuditTypes.cs` |
| Application | `Services/ShipService.cs`, `DTOs/Ship/ShipDtos.cs`, `Exceptions/ShipExceptions.cs`, `Validators/Ship/ShipRequestValidators.cs`, `Interfaces/Services/IShipService.cs`, `Interfaces/Services/IProductionUrlChecker.cs`, `Interfaces/Repositories/IShipAttemptRepository.cs`, `IShipCardRepository.cs`; sửa `NextBestActionService.cs`, `VerificationService.cs` (đọc mới), `IWorkspaceNotifier.cs` |
| Infrastructure | `Services/Ship/ProductionUrlChecker.cs`, `PublicAddress.cs`, `HostAddressResolver.cs`, `Persistence/Configurations/ShipConfiguration.cs`, `Repositories/ShipAttemptRepository.cs`, `ShipCardRepository.cs`, migration `AddShip` |
| API | `Controllers/ShipController.cs`, `Controllers/ShipCardsController.cs` |
| Tests | `Application/ShipServiceTests.cs`, `Domain/ScopeDisciplinePolicyTests.cs`, `Domain/ShipPoliciesTests.cs`, `Infrastructure/PublicAddressTests.cs`, `Infrastructure/ProductionUrlCheckerTests.cs`; thêm 5 test ở `NextBestActionServiceTests.cs` |

## 12. Kiểm thử

- **Unit:** 178 test mới (toàn bộ 1027/1027 đạt). `ProductionUrlChecker` được thử bằng DNS giả và HTTP giả: địa chỉ nội bộ, chuyển hướng vào trong, quá 3 chuyển hướng, chuyển sang `http`, SSL hỏng, file token thiếu, sai, chuyển sang host khác, đọc quá 1 KB, hết giờ.
- **Chạy thật:** 33 kiểm tra qua HTTP (chặn quyền, mọi điều kiện chặn, chặn SSRF với 5 địa chỉ nội bộ, kiểm thật `example.com` và ba trang lỗi SSL của `badssl.com`, giới hạn tần suất) và 22 kiểm tra cấp thẻ trên PostgreSQL thật (hai request cùng lúc, điểm, điểm neo, đọc lại, xác minh, công khai, sửa tay DB để thử phát hiện). Chi tiết ở `docs/ship-test-report.md`.

## 13. Cần chốt với thầy Vũ

1. Trọng số và trần của từng thành phần điểm (bảng mục 8 chỉ là nháp v0).
2. `DIRECT_PUSH_DETECTED` có tính vào điểm không.
3. Nâng ngưỡng có tính vào giới hạn 1 lần/7 ngày và 3 lần/dự án không (BUILD đang tính).
4. Luật S11 (mọi feature đã khóa phải được duyệt mới ship được) và S10 (chỉ ship version mới nhất): xác nhận đây là cách hiểu đúng của Path to Ship.

## 14. Giới hạn đã biết

- Ping và SSL chỉ chứng minh trang sống **lúc kiểm**.
- Bằng chứng sở hữu chứng minh đội kiểm soát tên miền, không chứng minh trang chạy đúng code của version đó.
- "Đã duyệt" nghĩa là bot đã đẩy code sang nhánh feature, không có nghĩa là đã vào `main` (ShipMate không ghi lúc merge PR). Merge gate của BUILD chỉ chặn được merge không qua ShipMate trên repo tổ chức.
- VERIFY không biết code đã đổi sau lúc tick xong (`04-verify.md` mục 15). Thẻ chỉ ghi lúc checklist hoàn tất.
- Điểm neo chứng minh lịch sử không bị sửa **sau** khi cấp thẻ, không chứng minh lịch sử đầy đủ.
- `open_drift` là trạng thái lúc cấp thẻ, không cắt theo điểm neo audit (cảnh báo không nằm trong chuỗi audit).
- `deadline_overrun` tính theo ngày UTC. Với múi giờ Việt Nam (UTC+7), ship từ 0 giờ tới 7 giờ sáng ngày hôm sau deadline vẫn tính là đúng hạn.
- `PENALTY_REFUND` (nhánh hotfix) chưa có, nên một Override hotfix hợp lệ vẫn bị trừ điểm.
- Giữa lần kiểm lại (S14) và lần lưu thẻ còn vài mili giây. Thẻ trùng thì index unique vẫn chặn, nhưng nếu đúng lúc đó có người bỏ tick một mục checklist thì thẻ vẫn được cấp. Khoảng thời gian rất ngắn nên chấp nhận.
- Chưa thử cấp thẻ qua HTTP với một website thật phục vụ token (mục 1).
