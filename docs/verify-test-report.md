# Báo cáo kiểm thử: luồng VERIFY

- **Ngày:** 2026-11-13
- **Unit test:** `dotnet test ShipMate.Tests` cho **828 / 828 đạt** (thêm 56 so với 772 trước VERIFY): `VerificationChecklistTests` 15, `VerificationServiceTests` 23, `VerifyRequestValidatorTests` 7, 4 test ở `FeatureChallengeServiceTests`, 1 ở `LockServiceTests`, 6 ở `NextBestActionServiceTests`.
- **Chạy thật:** API local trên PostgreSQL 18.6 (DB dev), 2 tài khoản thử của DB dev (một Manager, một Developer), workspace thử "LOCK test - Nha Khoa" (5 version, 4 feature đã khóa, chưa có submission). **Không gọi GitHub; đúng một lượt gọi Gemini, dùng để chấm điểm một challenge (mục 2b).**
- **Chưa commit:** toàn bộ code, test, migration và tài liệu còn trong working tree của `feature/build`.
- Kế hoạch: `docs/flows/04-verify.md`.

## 1. Migration và dữ liệu cũ

Migration `AddVerificationChecklists` chạy trên DB dev, kèm đoạn SQL chèn sẵn checklist cho snapshot có trước VERIFY. Kiểm bằng truy vấn chỉ đọc:

| Kiểm | Kết quả |
|---|---|
| Số snapshot, checklist, mục | 12, 12, 60 |
| Snapshot không có checklist | 0 |
| Checklist có số mục khác 5 | 0 |
| Đủ năm key (`main_user_journey`, `authentication`, `production_config`, `basic_crud`, `error_handling`) | Mỗi key 12 dòng |
| Version và workspace của checklist khớp snapshot | Khớp cả 12 |
| Chỉ mục | Hai unique (theo snapshot, theo workspace và version) và hai khóa chính |

## 2. Chạy thật qua HTTP (25 / 25 đạt)

| Nhóm | Kiểm | Kết quả |
|---|---|---|
| Đọc | Danh sách 5 version, mới nhất trước, chỉ v5 là `isLatest` | Đạt |
| Đọc | Chi tiết v5: 5 mục đúng thứ tự cố định, thiếu cả 5, `info` (4 feature khóa, 0 đã duyệt, 0 drift) | Đạt |
| Tick | Developer tick kèm ghi chú: có người tick, lúc tick, ghi chú | Đạt |
| Quyền | Developer bỏ qua một mục: `403 WORKSPACE_PERMISSION_DENIED` | Đạt |
| Quyền | Người ngoài workspace: `404 NOT_FOUND`; không có token: `401` | Đạt |
| Bỏ qua mục | Lý do 2 ký tự: `400 VALIDATION_FAILED` | Đạt |
| Bỏ qua mục | `main_user_journey`: `409 VERIFY_MAIN_JOURNEY_REQUIRED` | Đạt |
| Bỏ qua mục | Manager bỏ qua `authentication` kèm lý do: mục rời khỏi `missingItems` | Đạt |
| Bỏ qua mục | Tick mục đang bỏ qua: `409 VERIFY_ITEM_NOT_APPLICABLE`; bỏ tick mục đó không đổi gì | Đạt |
| Bỏ qua mục | Nói lại "áp dụng" cho mục đã tick: giữ nguyên tick | Đạt |
| Hoàn tất | Tick dần: chưa đủ thì `allItemsChecked: false`; mục cuối thì `true`, có `completedAt`, hết `missingItems` | Đạt |
| Mở lại | Bỏ tick một mục: `false` và `completedAt: null`; tick lại thì hoàn tất lại; ghi chú trống xóa ghi chú | Đạt |
| Từ chối | `itemKey` lạ: `400 VERIFY_ITEM_UNKNOWN`; version lạ: `404`; ghi chú trên 500 ký tự: `400` | Đạt |
| Cách ly | Tick ở v5 không đụng v4 | Đạt |
| NBA | Developer và Manager đều không bị gợi ý `CompleteVerification` khi chưa có feature nào được duyệt (nhận `ClaimFeature`) | Đạt |
| Đồng thời | 5 yêu cầu tick 5 mục khác nhau cùng lúc, hai người: 3 thắng (`200`), 2 `409 VERIFY_CHECKLIST_CONFLICT`, không lỗi khác; đúng ba mục của ba yêu cầu thắng được tick; `allItemsChecked` khớp các mục | Đạt |

## 2b. Duyệt một Feature Challenge qua HTTP (1 lượt gọi Gemini, đã duyệt chi phí)

Developer gửi challenge "Hiển thị số điện thoại phòng khám ở chân trang" (AI chấm **1**), Manager xác nhận điểm, hệ thống gợi ý `AddDirectly`, Manager duyệt. Đây là lượt gọi AI duy nhất của VERIFY; mọi bước khác không dùng AI.

| Kiểm | Kết quả |
|---|---|
| Duyệt sinh version 6 | Đạt (`resultingSnapshotVersion: 6`) |
| Version 6 có checklist **mới**: 5 mục, không mục nào đã tick hay bỏ qua, chưa hoàn tất | Đạt |
| Checklist của version 6 gắn đúng snapshot 6 theo id | Đạt (kiểm DB) |
| Version 5 giữ nguyên trạng thái (đã hoàn tất, `authentication` đang bỏ qua) | Đạt |
| `info` của version 6: 5 feature khóa, 0 đã duyệt, 0 drift | Đạt |
| Chuỗi audit sau lần duyệt | Nguyên vẹn (11 bản ghi) |

**Lần duyệt đầu bị cổng LOCK từ chối (`LOCK_GATE_FAILED`), không phải lỗi của VERIFY.** Workspace thử có sẵn một feature đã khóa độ phức tạp 5 ("Dong bo lich voi he thong benh vien khac", từ lần thử feature backup trước đây) trong khi kỹ năng cao nhất của đội là 4, nên cổng chặn mọi lần duyệt ở workspace này. Cách xử lý để không tốn thêm lượt AI: nâng **tạm** kỹ năng của tài khoản Manager thử lên 5, duyệt, rồi trả về 4 (đã kiểm DB: cả hai tài khoản đang ở 4). Ghi lại để lần sau thử ở workspace này không bị bất ngờ.

## 2c. Tiến độ feature, drift và gợi ý của NBA (ca A, 0 AI, 0 GitHub)

Workspace "Race test - Lock" (version 1, 4 feature khóa chưa ai làm). Submission `Approved` thật cần GitHub, nên dữ liệu thử được **ghi thẳng vào DB** trong một giao dịch, mỗi dòng có dấu `verify-test` để xóa đúng: 4 `feature_assignments` và 4 `build_submissions` trạng thái `approved` (một cho mỗi feature khóa), rồi một `drift_alerts` `unresolved`. Các kiểm tra còn lại đi qua HTTP thật.

| # | Kiểm | Kết quả |
|---|---|---|
| A0 | Trước khi chèn: `approvedFeatures 0`, NBA gợi ý `ClaimFeature` | Đạt |
| A1 | Sau khi có 4 submission đã duyệt: `info` `approvedFeatures 4`, `lockedFeatures 4`, `openDriftCount 0` | Đạt |
| A2 | NBA gợi ý `CompleteVerification`, `blockedReason: "verify_checklist_incomplete"`, `snapshotVersion: 1`, **không** có `blockedByFeatureId` | Đạt |
| A3 | Thêm một drift đang mở trên feature khóa: `openDriftCount 1`, `approvedFeatures` vẫn 4, checklist vẫn đọc và sửa được (drift không chặn) | Đạt |
| A4 | NBA ưu tiên drift của chính mình: `FixDrift` thay vì `CompleteVerification` | Đạt |
| A5 | Xóa drift, tick đủ 5 mục: `allItemsChecked: true`, NBA `NoAction` | Đạt |
| A6 | Bỏ tick một mục: NBA lại gợi ý `CompleteVerification` | Đạt |
| A7 | Dọn dữ liệu thử: assignment, submission, cảnh báo về 0; `approvedFeatures 0`, NBA về `ClaimFeature`; chuỗi audit nguyên vẹn | Đạt |

Đây là lần đầu phần đọc tiến độ, đếm drift theo feature và bước 6 của NBA chạy trên DB thật. Dữ liệu chèn tay **không** đi qua service nên không có bản ghi audit; chỉ còn các bản ghi `VERIFY_*` do việc tick thật.

## 3. Đối chiếu DB sau khi chạy

- Với cả 5 checklist của workspace, giá trị `all_items_checked` lưu sẵn **khớp** với việc "mọi mục áp dụng đã tick" tính lại từ các mục. Cột `completed_at` có đúng khi và chỉ khi hoàn tất.
- v5: `authentication` không áp dụng kèm lý do; ba mục còn lại và `main_user_journey` đã tick; ghi chú của `main_user_journey` còn, ghi chú đã xóa của `basic_crud` là `null`.
- Bản ghi audit đúng như thiết kế: **1** `VERIFY_ITEM_NOT_APPLICABLE` (lần Manager bỏ qua `authentication`; việc nói lại "áp dụng" cho mục đã áp dụng không ghi gì) và **2** `VERIFY_COMPLETED` (lần hoàn tất đầu, và lần hoàn tất lại sau khi bỏ tick).
- **Chuỗi audit của workspace nguyên vẹn** (10 bản ghi), tính lại bằng đúng hàm `AuditChain.FindFirstBroken` mà API dùng.
- `revision` của v5 là 9 (9 lần sửa ghi) và của v4 là 3 (đúng 3 lần sửa thắng), nên khóa chống sửa trùng đang hoạt động.
- Điều này cũng xác nhận EF ghi được các thuộc tính `private set` (`AllItemsChecked`, `CompletedAt`, `Revision`) vào DB.

## 4. Chưa kiểm chứng thật

| Việc | Lý do | Đã có |
|---|---|---|
| Duyệt challenge loại `RemoveAndReplace` và `Backup` qua HTTP | Đã chạy thật loại `AddDirectly` (mục 2b); hai loại kia dùng cùng đường tạo checklist | Unit test cho cả ba loại; cổng không qua thì không tạo |
| Lock lần đầu qua HTTP tạo checklist version 1 | Cần một workspace chưa Lock mà đã phân tích DEFINE xong | Unit test `Lock_AddsTheVersionOneChecklist_ForTheSnapshotItSealed`; migration đã tạo checklist cho mọi snapshot cũ |
| Submission đã duyệt qua **BUILD thật** (GitHub) rồi mới đọc `info` và NBA | Cần GitHub App thật; mục 2c dùng dữ liệu chèn tay cùng trạng thái `Approved` | Mục 2c chạy thật trên DB; BUILD tự có báo cáo riêng |
| Drift ở version trước vẫn tính khi feature còn khóa ở version sau; bỏ qua feature đã thay thế hoặc dự phòng | Cần nhiều version với drift mở | Unit test (`VerificationServiceTests`) |
| Chạy trên DB từ trạng thái trống (`database update` từ đầu) | DB dev đã có dữ liệu | Migration chạy trên DB có 12 snapshot và đạt |

## 5. Giới hạn đã biết

- Tick là tự khai. VERIFY không chứng minh sản phẩm chạy được.
- Không biết code đã đổi sau lúc đội tick xong (`04-verify.md` mục 15).
- 5 mục thiên về web app; sản phẩm không có giao diện dùng "không áp dụng".
- "Đã duyệt x/y feature" đếm theo submission `Approved` (bot đã đẩy nhánh feature), không phải theo việc đã vào `main`.
- Workspace đã Archive vẫn sửa được checklist, giống các luồng khác hiện tại.
- Không có endpoint HTTP kiểm chuỗi audit; chuỗi được kiểm bằng công cụ đọc DB.

## 6. Dữ liệu còn lại

- Workspace "LOCK test - Nha Khoa": checklist v5 đã hoàn tất (bỏ qua `authentication`), v4 có 3 mục đã tick, v6 mới tạo (chưa tick); thêm một challenge đã duyệt (version 6, feature "Hiển thị số điện thoại phòng khám ở chân trang"). Các bản ghi audit `VERIFY_*` và `SCOPE_CHANGE` của lần chạy này nằm trong chuỗi (chuỗi chỉ thêm được, không xóa được). Đây là workspace thử, không ảnh hưởng gì khác.
- Workspace "Race test - Lock": checklist v1 có 4 mục đã tick và còn 1 bản ghi audit `VERIFY_COMPLETED` (từ bước A5); dữ liệu chèn tay của ca A đã xóa hết (0 assignment, 0 submission, 0 cảnh báo).
- Một lời gọi Gemini đã dùng cho việc chấm điểm challenge.
