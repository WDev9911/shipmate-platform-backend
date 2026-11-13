# SHIP: kế hoạch cài đặt

Tài liệu này là **kế hoạch chi tiết cài đặt SHIP**, viết trước khi code, cho thành viên phụ trách SHIP. Nguồn: spec 2.5 của nhóm, đối chiếu với dữ liệu LOCK và BUILD đã có và với hợp đồng VERIFY trong `04-verify.md`.

Mục lục
1. Mục đích và phạm vi
2. Điều chỉnh so với spec 2.5 (và lý do)
3. Dữ liệu đã có sẵn để SHIP dùng
4. Dữ liệu mới
5. Trình tự
6. Kiểm tra URL an toàn (Ping, SSL, quyền sở hữu)
7. Time-to-Ship
8. Scope Discipline Score
9. Ship Card và xác minh công khai
10. API
11. Giai đoạn và file
12. Kiểm thử
13. Cần chốt với thầy Vũ
14. Giới hạn đã biết

---

## 1. Mục đích và phạm vi

Xác nhận một `snapshot_version` đã thật sự chạy trên production, tính hai chỉ số (Time-to-Ship, Scope Discipline Score) từ dữ liệu LOCK và BUILD đã ghi, và cấp một Ship Card mà người ngoài kiểm được là không bị sửa.

SHIP **không gọi AI**. Gọi mạng duy nhất là tới URL production do đội nhập.

## 2. Điều chỉnh so với spec 2.5 (và lý do)

| # | Spec 2.5 | Kế hoạch | Lý do |
|---|---|---|---|
| S1 | Bước 2 đọc `all_items_checked` | Gọi `GetReadinessAsync` của VERIFY, chặn khi `AllItemsChecked = false` | VERIFY trả thêm danh sách mục còn thiếu, đúng thứ spec cần hiển thị (`04-verify.md` D7) |
| S2 | Bước 3: Governance Suspended khi OAuth token bị revoke, yêu cầu re-authenticate GitHub | Cờ là `Workspace.GovernanceSuspendedAt`, bật khi **App ShipMate bị gỡ** khỏi repo; thông báo "ShipMate đã mất quyền với repo, cài lại ShipMate App" | Code BUILD đã chọn điều kiện này (OAuth token chỉ dùng để đăng nhập, không dùng để giữ repo). Cờ tự gỡ khi App được cài lại và có một lần submit mới |
| S3 | Ping 200 và SSL hợp lệ là đủ | Thêm **bước an toàn** trước khi gọi (chặn SSRF) và **bằng chứng sở hữu** URL | Không có hai bước này thì (a) server ShipMate bị dùng để gọi vào mạng nội bộ, (b) `https://google.com` cũng ship được, Ship Card mất ý nghĩa. Xem mục 6 |
| S4 | "Chạy lại đúng bước Ping + SSL" | Lưu mỗi lần thử vào `ship_attempts`; chạy lại là cập nhật đúng lần thử đó | Bước chạy lại cần trạng thái để biết đã qua bước 2–3 chưa |
| S5 | Ship nhiều lần, mỗi lần một thẻ | **Một Ship Card cho mỗi version** (unique `workspace_id, snapshot_version`) | Spec ship lại "mỗi lần Feature Challenge tạo version mới". Cho mint nhiều thẻ cho cùng version thì bấm đúp sinh thẻ trùng, và có thể "cày" thẻ |
| S6 | Công thức điểm chưa chốt | Lưu **điểm từng thành phần**, **phiên bản công thức**, **điểm neo audit** trên thẻ; trọng số nằm một chỗ | Công thức sẽ đổi sau khi hỏi thầy Vũ; thẻ cũ phải giải thích và tái tính được |
| S7 | "Ai cũng đối chiếu được hash" | Thêm endpoint công khai chỉ trả **thẻ còn khớp chuỗi hay không**, không lộ nội dung audit | Chuỗi audit là dữ liệu riêng của workspace (Admin còn không được xem); không có endpoint thì không ai đối chiếu được |
| S8 | "Dev" thực hiện SHIP | **Chỉ Manager** | Ship Card đại diện cả đội và công khai ra ngoài. Workspace một người thì Manager chính là dev |
| S9 | Ship Card "chia sẻ công khai" | Mặc định **riêng tư**; Manager bật công khai thì có đường link | Thẻ có tên sản phẩm và điểm kỷ luật của đội; công khai phải là lựa chọn |
| S10 | Không nói về version cũ | Cho ship version cũ hơn version mới nhất; điểm tính trên **toàn bộ lịch sử tới lúc ship**, drift chỉ đếm trong version đang ship (đúng spec) | Cùng cách Time-to-Ship tính từ mốc cố định: không thể chọn version cũ để né phạt đã phát sinh |

## 3. Dữ liệu đã có sẵn để SHIP dùng

| Cần | Đã có | Ở đâu |
|---|---|---|
| Mốc Time-to-Ship | `Workspace.FirstLockedAt`, ghi một lần ở Lock đầu, không ghi đè | `LockService` |
| Release | `MvpSnapshot.Version`, `Hash` | `mvp_snapshots` |
| Chặn khi mất quyền repo | `Workspace.GovernanceSuspendedAt`, `GovernanceSuspendedReason = APP_UNINSTALLED` | BUILD |
| Sẵn sàng | `IVerificationService.GetReadinessAsync(workspaceId, version)` trả `Exists`, `AllItemsChecked`, `MissingItems`, `ChecklistId`, `CompletedAt` | VERIFY (`04-verify.md` mục 9) |
| Đổi scope | `SCOPE_CHANGE` (hành động, `fromVersion`, `toVersion`) | `audit_trail` |
| Override | `SCOPE_BYPASS`; `SOLO_OVERRIDE_LOGGED` khi workspace một người | `audit_trail` |
| Hạ ngưỡng | `GOVERNANCE_DILUTION` (`from`, `to`) | `audit_trail` |
| Push lén | `DIRECT_PUSH_DETECTED` (`reason`) | `audit_trail` |
| Drift tồn đọng | `drift_alerts.SnapshotVersionAtCheck`, `Status` | `drift_alerts` |
| Tỉ lệ challenge | `feature_challenges.Action`, `Status = Approved` | `feature_challenges` |
| Deadline | `Workspace.LaunchDeadline` (có thể null) | `workspaces` |
| Neo và kiểm chuỗi | `audit_trail.Sequence`, `RecordHash`; `IAuditTrail.FindFirstBrokenAsync` | `AuditTrail` |

Chưa có: `PENALTY_REFUND` (thuộc nhánh hotfix, chưa làm). Đến lúc đó "Scope Bypass net" bằng tổng `SCOPE_BYPASS`.

## 4. Dữ liệu mới

**`ship_attempts`** (kết quả Ping/SSL; tách hẳn khỏi `verification_checklists` như spec yêu cầu)

| Cột | Ghi chú |
|---|---|
| `id`, `workspace_id`, `snapshot_version`, `created_by_user_id`, `created_at`, `updated_at` | |
| `url` | Đã chuẩn hóa (chỉ `https`, bỏ fragment) |
| `status` | `ChecksFailed`, `Shipped` |
| `ping_status_code`, `ping_error` | Mã HTTP cuối cùng hoặc mã lỗi của ShipMate (mục 6) |
| `ssl_valid`, `ssl_error`, `ssl_expires_at` | |
| `ownership_verified`, `ownership_error` | |
| `checked_at` | Lần chạy gần nhất |

Tối đa một lần thử chưa xong cho mỗi `(workspace_id, snapshot_version)`; lần thử mới thay lần cũ chưa xong.

**`ship_cards`**

| Cột | Ghi chú |
|---|---|
| `id`, `workspace_id`, `snapshot_version` | Unique `(workspace_id, snapshot_version)` (S5) |
| `ship_attempt_id`, `url`, `shipped_at`, `shipped_by_user_id` | |
| `product_name` | Chụp tên workspace lúc ship (đổi tên sau không làm đổi thẻ) |
| `time_to_ship_seconds` | |
| `score`, `score_breakdown` (jsonb), `score_formula_version` | S6 |
| `verification_checklist_id`, `verification_completed_at` | Từ VERIFY |
| `audit_anchor_sequence`, `audit_anchor_hash` | Điểm neo |
| `is_public`, `public_slug` | S9; slug ngẫu nhiên, không đoán được |

**`workspaces.ship_ownership_token`**: chuỗi ngẫu nhiên cấp một lần cho workspace (mục 6).

## 5. Trình tự

`POST .../ship/attempts` với `{ snapshotVersion, url }`, chỉ Manager:

1. Workspace đang hoạt động (không Archive), version tồn tại, version chưa có Ship Card (`SHIP_ALREADY_SHIPPED`).
2. VERIFY: `GetReadinessAsync`. Không có checklist hoặc `AllItemsChecked = false` thì `SHIP_VERIFY_INCOMPLETE` kèm `missingItems`. **Chặn cứng.**
3. `GovernanceSuspendedAt != null` thì `SHIP_GOVERNANCE_SUSPENDED`. **Chặn cứng.**
4. Kiểm URL (mục 6): an toàn, Ping, SSL, quyền sở hữu. Lưu `ship_attempts`. Hỏng bước nào thì trả `200` với `status = ChecksFailed` và lỗi cụ thể từng bước (đây là kết quả kiểm tra, không phải lỗi request).
5. Qua hết: trong **một** lần lưu
   - lấy điểm neo = bản ghi audit cuối của workspace (`Sequence`, `RecordHash`);
   - tính Time-to-Ship và điểm từ dữ liệu có `Sequence <= anchor` (mục 8);
   - tạo Ship Card, đặt attempt `Shipped`;
   - ghi audit `SHIP_CARD_MINTED` (id thẻ, version, điểm, điểm neo). Bản ghi này nằm **sau** điểm neo, nên lần ship sau tự nhiên neo cả việc ship trước.
6. Đua tranh: hai request cùng lúc thì ràng buộc unique của `ship_cards` cho một bên thắng; bên thua trả `SHIP_ALREADY_SHIPPED`.

`POST .../ship/attempts/{id}/recheck`: **chỉ** chạy lại bước 4 rồi bước 5 cho lần thử đó (spec bước 5). Bước 2 và 3 vẫn kiểm lại (rẻ, và trạng thái có thể đã đổi).

Chặn cứng: VERIFY chưa sẵn sàng, Governance Suspended, URL hỏng. Chỉ trừ điểm: mọi thứ ở mục 8 (đúng spec).

## 6. Kiểm tra URL an toàn

Server ShipMate tự gọi một URL do người dùng nhập, nên đây là chỗ dễ bị lợi dụng nhất của SHIP.

**An toàn (chặn SSRF), làm trước mọi lần gọi:**
- Chỉ nhận `https://`, cổng 443, không có user/password trong URL (`SHIP_URL_INVALID`).
- Phân giải DNS rồi từ chối nếu **bất kỳ** địa chỉ nào thuộc loopback, private (10/8, 172.16/12, 192.168/16), link-local (169.254/16, gồm metadata của cloud), CGNAT (100.64/10), multicast, và các dải tương ứng của IPv6 (`SHIP_URL_NOT_PUBLIC`).
- Kiểm **lúc kết nối** (`SocketsHttpHandler.ConnectCallback` kết nối thẳng vào đúng IP đã kiểm), không chỉ lúc phân giải, để chống DNS rebinding.
- Không tự theo chuyển hướng; tự theo tối đa 3 lần, mỗi lần kiểm lại như trên.
- Timeout 10 giây, đọc tối đa vài KB, **không** trả nội dung phản hồi về cho người dùng.
- Giới hạn tần suất: tối đa 10 lần kiểm mỗi workspace mỗi giờ.

**Ping:** sau tối đa 3 chuyển hướng, mã cuối phải là `200` (`SHIP_PING_FAILED`, kèm mã thật).

**SSL:** chứng chỉ hợp lệ theo kiểm tra mặc định của .NET (chuỗi tin cậy, đúng tên miền, còn hạn). Lưu ngày hết hạn. Lỗi thì `SHIP_SSL_INVALID` kèm lý do.

**Quyền sở hữu (S3):** trang phải phục vụ `https://<host>/.well-known/shipmate.txt` có nội dung đúng `ship_ownership_token` của workspace (`SHIP_OWNERSHIP_UNVERIFIED`). Token hiện ở màn hình SHIP; đội thêm một file tĩnh vào bản deploy. Không làm bước này thì Ship Card chỉ chứng minh "có một trang web đang chạy", không phải "sản phẩm này đang chạy".

## 7. Time-to-Ship

`shipped_at − Workspace.FirstLockedAt`, lưu bằng giây. Không tính từ lúc tạo version đang ship (đúng spec, chống "reset đồng hồ" bằng một Feature Challenge nhỏ). Mọi lần ship sau dùng cùng mốc. `FirstLockedAt` null (không thể xảy ra khi đã có snapshot) thì từ chối, không đoán.

## 8. Scope Discipline Score

Đặt trong một chính sách thuần `ScopeDisciplinePolicy` (Domain): đầu vào là các con số đã đếm, đầu ra là điểm và từng thành phần. Service đếm, chính sách tính; đổi trọng số chỉ sửa một file và tăng `score_formula_version`.

**Đếm (chỉ bản ghi audit có `Sequence <= anchor`):**

| Thành phần | Đếm |
|---|---|
| Volatility | Số `SCOPE_CHANGE` |
| Bypass net | Số `SCOPE_BYPASS` (trừ `PENALTY_REFUND` khi có) |
| Dilution | Số `GOVERNANCE_DILUTION` |
| Drift tồn đọng | Hàm mới của SHIP `IDriftAlertRepository.CountOpenAsync(workspaceId, version)`: cảnh báo có `SnapshotVersionAtCheck = version đang ship` và `Status` là `Unresolved` hoặc `Acknowledged`, đúng như spec. (VERIFY hiển thị drift theo **feature** của version, không theo `SnapshotVersionAtCheck`, nên hai con số có thể khác nhau; xem `04-verify.md` D4. Nhóm nên chốt một cách đếm nếu muốn hai màn hình khớp.) Lưu ý: hàm này đếm trạng thái **hiện tại**, không cắt theo điểm neo audit |
| Remove & Replace | `RemoveAndReplace / (RemoveAndReplace + AddDirectly)` trên challenge `Approved`; `Backup` không tính; mẫu số 0 thì tỉ lệ 0 |
| Trễ deadline | Số ngày `shipped_at` sau `LaunchDeadline`; deadline null thì 0 |
| Push lén (ngoài spec) | Số `DIRECT_PUSH_DETECTED` |

Lưu ý khi đếm drift: một cảnh báo chuyển `Resolved` có thể là vì feature **drift lần nữa** (`ResolvedBySubmissionId` trỏ tới một submission `Drift`), không phải vì đã sửa. Cảnh báo mới khi đó vẫn `Unresolved` nên được đếm; chỉ không đếm trùng cảnh báo cũ.

**Công thức nháp v0 (để code chạy được; phải chốt với thầy Vũ, mục 13):** bắt đầu 100, trừ theo bảng, mỗi thành phần có trần, kết quả kẹp trong 0–100.

| Thành phần | Trừ | Trần |
|---|---|---|
| Volatility | 3 mỗi lần | 15 |
| Bypass net | 5 mỗi lần | 25 |
| Dilution | 10 mỗi lần | 20 |
| Drift tồn đọng | 5 mỗi cảnh báo | 15 |
| Remove & Replace | tỉ lệ × 10 | 10 |
| Trễ deadline | 1 mỗi ngày | 15 |
| Push lén | 5 mỗi lần | 10 |

Trần giữ cho một loại lỗi không tự kéo điểm về 0, và giúp mỗi thành phần đọc được trên thẻ ("Override: −10/25").

`score_breakdown` lưu từng thành phần: số đếm, điểm trừ, trần. Thẻ hiện được "vì sao điểm này".

## 9. Ship Card và xác minh công khai

Thẻ hiện: tên sản phẩm, version, Time-to-Ship, điểm (kèm từng thành phần), ngày ship, URL, và điểm neo.

**Xác minh** `GET /api/ship-cards/{slug}/verify` (không cần đăng nhập, chỉ cho thẻ công khai): tính lại chuỗi bằng `FindFirstBrokenAsync`, rồi trả

```json
{ "valid": true, "anchorSequence": 412, "anchorHash": "…", "checkedAt": "…" }
```

`valid = true` khi chuỗi nguyên vẹn **tới ít nhất** `anchorSequence` và `RecordHash` ở `anchorSequence` đúng bằng `audit_anchor_hash` trên thẻ. Không trả bất kỳ nội dung audit nào. Cần thêm một hàm đọc vào `IAuditTrail`: lấy bản ghi cuối (neo khi mint) và lấy `RecordHash` ở một `Sequence` (xác minh).

Nói đúng mức: điểm neo chứng minh **lịch sử không bị sửa sau khi thẻ được cấp**. Nó không chứng minh lịch sử đầy đủ hay trung thực ngay từ đầu (ví dụ push lén mà GitHub không báo thì không có bản ghi).

## 10. API

Route gốc `api/workspaces/{workspaceId}/ship`.

| Method | Route | Ai | Việc |
|---|---|---|---|
| GET | `ownership-token` | Manager | Token và đường dẫn file cần đặt |
| POST | `attempts` | Manager | Mục 5 |
| POST | `attempts/{attemptId}/recheck` | Manager | Chạy lại kiểm URL |
| GET | `attempts` | Thành viên | Lịch sử lần thử |
| GET | `cards` | Thành viên | Các Ship Card của workspace |
| PUT | `cards/{cardId}/visibility` | Manager | `{ "isPublic": bool }` |
| GET | `/api/ship-cards/{slug}` | Ai cũng được | Thẻ công khai |
| GET | `/api/ship-cards/{slug}/verify` | Ai cũng được | Mục 9 |

Mã lỗi mới: `SHIP_ALREADY_SHIPPED` (409), `SHIP_VERIFY_INCOMPLETE` (409), `SHIP_GOVERNANCE_SUSPENDED` (409), `SHIP_URL_INVALID` (400), `SHIP_RATE_LIMITED` (429). Lỗi của từng bước kiểm (`SHIP_URL_NOT_PUBLIC`, `SHIP_PING_FAILED`, `SHIP_SSL_INVALID`, `SHIP_OWNERSHIP_UNVERIFIED`) nằm **trong** kết quả lần thử, không phải mã HTTP lỗi.

Sự kiện SignalR `ShipCardMinted` tới mọi thành viên (cùng hub `/hubs/workspace` của BUILD).

## 11. Giai đoạn và file

| GĐ | Việc | File chính |
|---|---|---|
| 1 | Chờ VERIFY có `GetReadinessAsync` (có thể dùng bản giả để bắt đầu) | — |
| 2 | Domain: entity, `ScopeDisciplinePolicy`, `AuditTypes.ShipCardMinted` | `Entities/ShipAttempt.cs`, `Entities/ShipCard.cs`, `Policies/ScopeDisciplinePolicy.cs` |
| 3 | DB: cấu hình, repository, migration, cột `ship_ownership_token`, hàm `CountOpenAsync` | `Configurations/Ship*Configuration.cs`, `Repositories/Ship*Repository.cs`, `DriftAlertRepository.cs`, migration `AddShip` |
| 4 | Kiểm URL an toàn (Infrastructure, có interface để test giả) | `Services/Ship/ProductionUrlChecker.cs`, `Interfaces/Services/IProductionUrlChecker.cs` |
| 5 | Đếm điểm, service, controller, lỗi | `Services/ShipService.cs`, `Services/ShipScoreCalculator.cs`, `API/Controllers/ShipController.cs`, `ShipCardsController.cs`, `Exceptions/ShipExceptions.cs` |
| 6 | `IAuditTrail`: đọc bản ghi cuối và hash theo `Sequence` | `AuditTrail.cs` |
| 7 | Chạy thật, tài liệu | `frontend-guide.md`, báo cáo test SHIP |

## 12. Kiểm thử

**Unit:**
- `ScopeDisciplinePolicy`: từng thành phần, trần, kẹp 0–100, deadline null, mẫu số tỉ lệ bằng 0.
- Kiểm URL: chặn `http://`, IP private/loopback/link-local/IPv6 tương ứng, tên miền phân giải ra IP nội bộ, chuyển hướng sang IP nội bộ, quá 3 chuyển hướng; ping khác 200; SSL hỏng; file sở hữu sai hoặc thiếu.
- Service: chặn đúng thứ tự (VERIFY, Governance), chỉ Manager, thẻ trùng version, đua tranh, chỉ đếm audit tới điểm neo, ghi `SHIP_CARD_MINTED` sau điểm neo.
- Xác minh: chuỗi nguyên vẹn thì `valid`; sửa một bản ghi trước điểm neo thì `invalid`; bản ghi thêm sau điểm neo không làm hỏng thẻ cũ; thẻ riêng tư trả 404.

**Chạy thật:** một trang tĩnh có HTTPS (ví dụ GitHub Pages hoặc Vercel miễn phí) chứa file sở hữu; ship một version đã VERIFY; thử URL `http://`, `https://localhost`, một tên miền chưa có file, một trang trả 404; xác minh thẻ; sửa tay một bản ghi audit trên DB dev rồi xác minh lại phải ra `invalid`.

## 13. Cần chốt với thầy Vũ

1. Trọng số và trần của từng thành phần điểm (bảng mục 8 chỉ là nháp v0).
2. `DIRECT_PUSH_DETECTED` có tính vào điểm không.
3. Nâng ngưỡng có tính vào giới hạn 1 lần/7 ngày và 3 lần/dự án không (BUILD đang tính).

## 14. Giới hạn đã biết

- Ping và SSL chỉ chứng minh trang sống **lúc kiểm**.
- VERIFY không biết code đã đổi sau lúc đội tick xong (`04-verify.md` mục 15), nên Ship Card chỉ ghi lúc checklist hoàn tất, không bảo đảm checklist khớp đúng bản đang chạy.
- Bằng chứng sở hữu chứng minh đội kiểm soát tên miền, không chứng minh trang chạy đúng code của version đó.
- Điểm neo chứng minh lịch sử không bị sửa **sau** khi cấp thẻ, không chứng minh lịch sử đầy đủ.
- `PENALTY_REFUND` chưa có, nên Override hotfix hợp lệ vẫn bị trừ điểm.
