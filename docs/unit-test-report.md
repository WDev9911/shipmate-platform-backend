# Báo cáo Unit Test — luồng DEFINE

**Kết quả: 183 test, 183 pass, 0 fail** (117 test DEFINE ban đầu, 30 test phân tích AI của DEFINE ở mục 10–11, 36 test Auth ở mục 12; cập nhật 2026-10-19). Chạy bằng `dotnet test ShipMate.Tests`. Không dùng DB, không gọi AI thật: repository được giả lập bằng NSubstitute hoặc lưu trong bộ nhớ, AI được thay bằng câu trả lời soạn sẵn. Cả project hiện có 359 test (gồm cả LOCK, xem `docs/lock-test-report.md`).

**Ký hiệu:** `AI` = from_ai, `MEN` = from_customer_mentioned, `COM` = from_committed_list. `Inc` = included, `Exc` = excluded, `Pen` = pending_confirmation. "Dev" = Manager của workspace. Lỗi được viết theo tên exception; "không lưu" nghĩa là `SaveChanges` không được gọi.

---

## 1. Bảng trạng thái và nguồn gốc (13 test)
File: `Domain/FeatureStatusAndOriginPolicyTests.cs`

| # | Input | Kết quả mong đợi |
|---|---|---|
| 1 | origin AI, verdict required | Inc |
| 2 | origin AI, verdict not_required | Exc (vào Kill List) |
| 3 | origin MEN, verdict required | Pen |
| 4 | origin MEN, verdict not_required | Pen |
| 5 | origin COM, verdict required | Pen |
| 6 | origin COM, verdict not_required | Pen |
| 7 | origin AI, không có verdict | lỗi `ArgumentException` |
| 8 | origin COM, không có verdict | Pen |
| 9 | sources [AI, MEN] (thử cả 2 thứ tự) | origin = MEN |
| 10 | sources [AI, COM] | origin = COM |
| 11 | sources [MEN, COM] | origin = COM |
| 12 | sources [AI, AI] | origin = AI |
| 13 | sources rỗng | lỗi `ArgumentException` |

## 2. Gộp feature và quyền sửa/loại feature đã chốt (15 test)
File: `Domain/FeatureMergeAndChangePolicyTests.cs`

| # | Input | Kết quả mong đợi |
|---|---|---|
| 1 | gộp MEN (vị trí 0) với COM (vị trí 5) | COM giữ nội dung, MEN bị gộp vào |
| 2 | gộp AI với MEN (thử cả 2 thứ tự đối số) | MEN giữ nội dung |
| 3 | gộp 2 feature cùng origin, vị trí 1 và 2 | feature đứng trước giữ nội dung |
| 4 | COM + đổi sang Exc | cần change request |
| 5 | COM + đổi sang Inc | không cần |
| 6 | COM + đổi sang Pen | không cần |
| 7 | MEN + đổi sang Exc | không cần |
| 8 | AI + đổi sang Exc | không cần |
| 9 | sửa nội dung feature COM | cần change request |
| 10 | sửa nội dung feature MEN | không cần |
| 11 | sửa nội dung feature AI | không cần |
| 12 | COM đổi Exc → Inc | ghi audit |
| 13 | COM đổi Pen → Inc | không ghi audit (lần đầu) |
| 14 | COM đổi Inc → Inc | không ghi audit |
| 15 | AI đổi Exc → Inc | không ghi audit |

## 3. Kill List, đếm feature, chạy lại AI (9 test)
File: `Domain/FeatureListPolicyTests.cs`

| # | Input | Kết quả mong đợi |
|---|---|---|
| 1 | 4 feature: Exc; MEN Pen + not_required; MEN Pen + required; Inc | Kill List gồm 2 feature đầu, 2 feature sau không |
| 2 | 3 primary (Inc, Pen, Exc), 2 supporting (Inc, Exc) | core = 2, supporting = 1 |
| 3 | số core = 0 | không vượt gợi ý |
| 4 | số core = 5 | không vượt gợi ý |
| 5 | số core = 6 | vượt gợi ý 3–5 |
| 6 | AI, dev_decided = false | AI được sinh lại |
| 7 | AI, dev_decided = true | giữ nguyên |
| 8 | MEN, dev_decided = false | giữ nguyên |
| 9 | COM, dev_decided = false | giữ nguyên |

## 4. Đồ thị phụ thuộc, phát hiện vòng lặp (6 test)
File: `Domain/FeatureDependencyGraphTests.cs`

| # | Input | Kết quả mong đợi |
|---|---|---|
| 1 | b→a, c→b, c→a (không vòng) | không có vòng |
| 2 | a→b, b→c, c→a | trả vòng khép kín 4 phần tử (a,b,c,a) |
| 3 | a→a | là vòng |
| 4 | b→a có sẵn, thêm a→b | trả vòng [a,b,a] |
| 5 | b→a có sẵn, thêm c→b | an toàn |
| 6 | c→b, b→a có sẵn, thêm a→c | phát hiện vòng gián tiếp |

## 5. Điều kiện READY_FOR_LOCK (5 test)
File: `Domain/ProductDefinitionReadinessPolicyTests.cs`

| # | Input | Kết quả mong đợi |
|---|---|---|
| 1 | 1 feature Inc, 1 feature Exc | sẵn sàng |
| 2 | 1 feature MEN đang Pen | chưa sẵn sàng, danh sách pending có đúng feature đó |
| 3 | 1 feature gắn possible_duplicate, 1 feature gắn persona_conflict (đang Inc) | chưa sẵn sàng, mỗi danh sách cờ có 1 phần tử |
| 4 | feature Exc nhưng vẫn gắn cả 2 cờ | vẫn sẵn sàng (loại feature coi như đã xử lý cờ) |
| 5 | a→b và b→a | chưa sẵn sàng, có vòng lặp |

## 6. Product Definition và Constraints (7 test)
File: `Domain/ProductDefinitionTests.cs`

| # | Input | Kết quả mong đợi |
|---|---|---|
| 1 | sửa khi đang READY_FOR_LOCK | về Draft, cập nhật `UpdatedAt` |
| 2 | sửa khi đang Draft | vẫn Draft |
| 3 | feature có vị trí 4 và 2 | vị trí kế tiếp = 5; chưa có feature thì = 0 |
| 4 | constraints cả 3 phần null | rỗng |
| 5 | constraints "" và "  " | rỗng |
| 6 | chỉ có customer = "rule" | không rỗng |
| 7 | chỉ có team = "rule" | không rỗng |

## 7. Dev review: đổi trạng thái, sửa tay, gộp, cờ, dependency, change request (33 test)
File: `Application/FeatureReviewServiceTests.cs`

**Quyền và điều kiện trước**

| # | Input | Kết quả mong đợi |
|---|---|---|
| 1 | review khi chưa có lần phân tích AI nào thành công | `ProductDefinitionNotAnalyzedException` |
| 2 | người gọi không phải Manager | `WorkspacePermissionDeniedException`, trạng thái feature không đổi, không lưu |
| 3 | featureId không tồn tại | `NotFoundException` |

**Đổi trạng thái (Bước 5, 7, 8)**

| # | Input | Kết quả mong đợi |
|---|---|---|
| 4 | INCLUDE feature MEN đang Pen, definition đang READY | thành Inc, `dev_decided = true`, definition về Draft, có lưu |
| 5 | EXCLUDE feature COM | `CommittedFeatureChangeRequiredException`, giữ Pen |
| 6 | INCLUDE feature COM đang Pen | thành Inc, không ghi audit |
| 7 | INCLUDE lại feature COM đang Exc | thành Inc, ghi 1 audit loại Include kèm người thực hiện |

**Loại feature có phụ thuộc (Bước 5)**

| # | Input | Kết quả mong đợi |
|---|---|---|
| 8 | loại prerequisite khi có dependent đang Inc, không chọn cách xử lý | `FeatureHasDependentsException` liệt kê đúng dependent, prerequisite vẫn Inc |
| 9 | như trên, chọn "xóa liên kết" | prerequisite Exc, dependent vẫn Inc và `dev_decided = true`, link bị xóa |
| 10 | chuỗi root ← middle ← leaf, chọn "loại luôn dependent" | cả 3 feature thành Exc |
| 11 | như trên nhưng dependent là feature COM | `CommittedFeatureChangeRequiredException`, không feature nào đổi |
| 12 | dependent đã Exc từ trước | loại bình thường, không hỏi cách xử lý |

**Sửa tay (Bước 5)**

| # | Input | Kết quả mong đợi |
|---|---|---|
| 13 | sửa feature AI, chỉ gửi `name` | chỉ đổi name, giữ description và scope, `dev_decided = true` |
| 14 | sửa feature COM | `CommittedFeatureChangeRequiredException`, tên giữ nguyên |

**Yêu cầu thay đổi feature đã chốt (Bước 8)**

| # | Input | Kết quả mong đợi |
|---|---|---|
| 15 | sửa "Online Payment" thành "Online Payment (VNPay + MoMo)", có lý do, đã tick báo khách | tên đổi, definition về Draft, ghi audit: action Edit, lý do, cờ báo khách, nội dung cũ/mới |
| 16 | loại feature COM, có lý do và tick xác nhận | thành Exc, ghi audit action Exclude |
| 17 | loại feature COM đã Exc | `FeatureAlreadyExcludedException` |
| 18 | gửi change request cho feature AI | `FeatureNotCommittedException` |

**Trùng lặp (Bước 5, 6)**

| # | Input | Kết quả mong đợi |
|---|---|---|
| 19 | gộp MEN (gắn cờ trùng) với COM | COM giữ tên "Online Payment", origin COM, sources chứa cả MEN và COM, `dev_decided = true`, MEN bị xóa, ghi audit Merge |
| 20 | gộp MEN với AI (không có COM) | MEN giữ nội dung, AI bị xóa, không ghi audit |
| 21 | feature khác đang phụ thuộc vào feature bị gộp | link được chuyển sang feature còn lại, link cũ bị xóa |
| 22 | gộp feature không gắn cờ | `FeatureNotFlaggedAsDuplicateException` |
| 23 | "Giữ riêng" | xóa cờ, `dev_decided = true` cho cả 2 feature, không feature nào bị xóa |

**Xung đột persona**

| # | Input | Kết quả mong đợi |
|---|---|---|
| 24 | "Giữ nguyên, đây là ngoại lệ" trên feature gắn cờ | xóa cờ và lý do, `dev_decided = true` |
| 25 | làm tương tự trên feature không gắn cờ | `FeatureNotFlaggedAsPersonaConflictException` |

**Thêm / xóa dependency**

| # | Input | Kết quả mong đợi |
|---|---|---|
| 26 | b phụ thuộc a, lý do "needs a" | thêm link đúng, `dev_decided = true` cho cả a và b |
| 27 | a phụ thuộc chính a | `FeatureSelfDependencyException` |
| 28 | thêm link đã có | `DependencyAlreadyExistsException` |
| 29 | feature Inc phụ thuộc feature Exc | `DependencyOnExcludedFeatureException` |
| 30 | b→a có sẵn, thêm a→b | `DependencyCreatesCycleException` (vòng 3 phần tử), không ghi link |
| 31 | phụ thuộc vào feature không tồn tại | `NotFoundException` |
| 32 | xóa link có sẵn | link bị xóa, `dev_decided = true` cho cả 2 đầu |
| 33 | xóa link không tồn tại | `NotFoundException` |

## 8. Product Definition Service (20 test)
File: `Application/ProductDefinitionServiceTests.cs`

**Chuyển READY_FOR_LOCK**

| # | Input | Kết quả mong đợi |
|---|---|---|
| 1 | chưa có product definition | `ProductDefinitionNotAnalyzedException` |
| 2 | có nhưng chưa phân tích AI lần nào | `ProductDefinitionNotAnalyzedException` |
| 3 | còn 1 feature MEN đang Pen | `ProductDefinitionNotReadyException`, vẫn Draft, không lưu |
| 4 | a→b và b→a | `ProductDefinitionNotReadyException` |
| 5 | mọi điều kiện đã đạt | thành READY_FOR_LOCK, có lưu |
| 6 | đã READY sẵn | không lưu lại |

**Report**

| # | Input | Kết quả mong đợi |
|---|---|---|
| 7 | xem report khi chưa phân tích | `ProductDefinitionNotAnalyzedException` |
| 8 | 6 primary, 1 supporting, 1 feature Exc | primary = 6, supporting = 1, khuyến nghị 3–5, vượt khuyến nghị = true |

**Feature đã chốt (Bước 1)**

| # | Input | Kết quả mong đợi |
|---|---|---|
| 9 | thêm "Online Payment" khi chưa có definition | tạo definition mới, feature mới origin COM, Pen, chưa có role, vị trí 0 |
| 10 | thêm khi đã có feature ở vị trí 3 và definition đang READY | vị trí = 4, definition về Draft |
| 11 | sửa trực tiếp sau lần phân tích đầu | `CommittedFeatureLockedException`, tên giữ nguyên |
| 12 | sửa trực tiếp trước lần phân tích đầu | đổi tên thành công |
| 13 | xóa trực tiếp trước lần phân tích đầu | xóa thành công |
| 14 | xóa trực tiếp sau lần phân tích đầu | `CommittedFeatureLockedException`, không xóa |
| 15 | xóa feature không phải feature đã chốt | `NotFoundException` |

**Constraints (mới)**

| # | Input | Kết quả mong đợi |
|---|---|---|
| 16 | customer = "  HIPAA only  ", company = "   ", team = null | lưu customer = "HIPAA only" (đã cắt khoảng trắng), company và team = null |
| 17 | definition đã có constraints, gửi cả 3 phần trống | constraints = null |
| 18 | chưa có definition, gửi team = "rule" | tạo definition mới chứa constraints, có lưu |
| 19 | sửa constraints khi đang READY_FOR_LOCK | về Draft |
| 20 | người gọi không phải Manager | `WorkspacePermissionDeniedException`, không lưu |

## 9. Validator (9 test)
File: `Application/ValidatorTests.cs`

| # | Input | Kết quả mong đợi |
|---|---|---|
| 1 | change request: Exclude, có lý do, tick xác nhận | hợp lệ |
| 2 | lý do rỗng | không hợp lệ |
| 3 | chưa tick xác nhận báo khách | không hợp lệ |
| 4 | action = Include | không hợp lệ (chỉ cho Edit hoặc Exclude) |
| 5 | action = Edit, không đổi name/description/scope | không hợp lệ |
| 6 | action = Edit, có name mới | hợp lệ |
| 7 | constraints: không gửi gì | hợp lệ (cả 3 phần đều tùy chọn) |
| 8 | team dài 5001 ký tự | không hợp lệ |
| 9 | company dài đúng 5000 ký tự | hợp lệ |

---

## 10. Kiểm tra output của AI trước khi lưu (16 test)
File: `Application/DefineAnalysisOutputValidatorTests.cs`

Quyết định M3 số 4: output vi phạm luật thì hủy cả lần chạy. Mỗi test đưa vào một output sai đúng một luật.

| # | Input | Kết quả mong đợi |
|---|---|---|
| 1 | output đúng: feature đã chốt cần 1 feature `supporting` mới | không lỗi |
| 2 | persona chính rỗng | lỗi persona |
| 3 | 2 feature trùng id | lỗi id dùng 2 lần |
| 4–5 | tên rỗng; tên dài 201 ký tự | lỗi tên |
| 6 | feature AI tự nhận `from_committed_list` | lỗi origin |
| 7 | feature đã chốt bị AI trả về với origin khác | lỗi origin |
| 8 | thiếu feature đã chốt | lỗi thiếu feature đã chốt |
| 9 | phụ thuộc vào id không tồn tại | lỗi feature không tồn tại |
| 10 | phụ thuộc vào feature giữ nguyên mà AI không trả về | hợp lệ (M8 số 18) |
| 11 | feature phụ thuộc chính nó | lỗi tự phụ thuộc |
| 12–13 | cờ trùng lặp mà `duplicate_of` rỗng; hoặc trỏ tới feature không phải đã chốt | lỗi `duplicate_of` |
| 14 | cờ trùng lặp trỏ đúng feature đã chốt | hợp lệ |
| 15 | feature `supporting` mới không feature `primary` nào cần | lỗi (Bước 2.2) |
| 16 | feature `supporting` giữ nguyên không feature `primary` nào cần | hợp lệ (M3 số 3) |

## 11. Phân tích và chạy lại AI (14 test)
File: `Application/ProductDefinitionAnalysisServiceTests.cs`

AI giả trả JSON soạn sẵn. Repository lưu trong bộ nhớ, ghi lại feature và liên kết được thêm/xóa để kiểm tra đúng cái gì thay đổi.

| # | Input | Kết quả mong đợi |
|---|---|---|
| 1 | lần đầu: 1 feature COM, AI trả thêm 1 MEN `supporting`, 1 AI `required`, 1 AI `not_required` | COM giữ tên và trạng thái, nhận `role` và cờ lệch persona. Feature mới: MEN → Pen, AI required → Inc, AI not_required → Exc, `sources = [origin]`. Id của AI được đổi thành id thật cho liên kết. Lưu persona, problem/solution và lần chạy (thành công, model, Vision Prompt, raw response) |
| 2 | lần đầu: AI gắn cờ trùng lặp với feature COM | `DuplicateOfId` trỏ đúng feature COM |
| 3 | lần đầu | input gửi AI có feature đã chốt, **không** có `existing_features` và `current_persona` |
| 4 | AI trả JSON hỏng | `AiOutputInvalidException`. Chỉ lưu lần chạy thất bại (lý do, raw response). Không feature, persona hay `role` nào thay đổi |
| 5 | AI trả output vi phạm luật `supporting` | như trên, lý do ghi trên lần chạy |
| 6 | AI bị giới hạn lượt gọi | lần chạy thất bại ghi `AI_RATE_LIMITED`, lỗi được ném tiếp |
| 7 | chạy lại có `instruction` và `reassessPersona` | input gửi AI có `existing_features` (kèm `can_regenerate`), `current_persona`, `instruction`, `reassess_persona` (M8 số 17) |
| 8 | chạy lại: 2 feature AI chưa ai đụng, AI chỉ trả 1 | feature được trả về sửa tại chỗ (cùng id); feature còn lại bị bỏ |
| 9 | chạy lại: feature MEN phụ thuộc 1 feature AI bị bỏ | liên kết bị xóa và báo trong `removedDependencies` (M8 số 21) |
| 10 | chạy lại: feature `dev_decided`, AI đổi tên, role, verdict | chỉ `ai_assessment` đổi; tên, trạng thái, role giữ nguyên |
| 11 | chạy lại: feature COM `dev_decided` nhưng AI chưa từng đánh giá | vẫn nhận `role` và cờ lệch persona (M8 số 19) |
| 12 | chạy lại: AI bỏ sót feature MEN | feature vẫn còn (M8 số 18) |
| 13 | chạy lại: liên kết giữa 2 feature giữ nguyên; AI bỏ liên kết đó và đề xuất liên kết ngược | giữ nguyên liên kết của Dev, không thêm/xóa liên kết nào (M8 số 20) |
| 14 | chạy lại khi đang READY_FOR_LOCK | về Draft (M7 số 13) |

## 12. Auth (36 test)
File: `Application/AuthServiceTests.cs`

Mã OTP được lấy từ nội dung email giả, rồi so với hash đã lưu, để chắc chắn mã gửi cho người dùng đúng là mã hệ thống kiểm tra.

| # | Input | Kết quả mong đợi |
|---|---|---|
| 1 | đăng ký email mới | user `developer`, chưa xác thực, mật khẩu đã hash. Mã OTP trong email khớp hash đã lưu, hết hạn sau 15 phút |
| 2 | đăng ký email đã có | `EmailAlreadyExistsException`, không tạo user, không gửi email |
| 3 | xác thực đúng mã | user được xác thực, mã bị đánh dấu đã dùng, đăng nhập luôn (refresh token lưu dạng hash) |
| 4–7 | xác thực sai mã; mã hết hạn; mã đã dùng; email không tồn tại | `InvalidOrExpiredTokenException`, user vẫn chưa xác thực |
| 8 | gửi lại mã cho user chưa xác thực | vô hiệu mã cũ, gửi mã mới khớp hash |
| 9–10 | gửi lại mã cho email không tồn tại hoặc đã xác thực | không làm gì, không báo lỗi (không lộ tài khoản) |
| 11 | đăng nhập đúng | cấp token, ghi thiết bị |
| 12 | sai mật khẩu và email không tồn tại | cùng một lỗi `InvalidCredentialsException` |
| 13 | tài khoản chỉ có GitHub, không mật khẩu | `InvalidCredentialsException` |
| 14 | tài khoản bị khóa | `AccountLockedException` |
| 15 | email chưa xác thực | `EmailNotVerifiedException` |
| 16 | refresh token hợp lệ | xoay vòng: token cũ bị thu hồi, trỏ tới token mới, giữ thiết bị |
| 17 | refresh token hết hạn hoặc không tồn tại | `InvalidOrExpiredTokenException` |
| 18 | dùng lại refresh token đã thu hồi | thu hồi mọi phiên của user (dấu hiệu bị đánh cắp) |
| 19 | đăng xuất 2 lần, đăng xuất token lạ | thu hồi đúng 1 lần, không báo lỗi |
| 20–21 | quên mật khẩu với email không tồn tại hoặc tài khoản chỉ có GitHub | không làm gì, không báo lỗi |
| 22 | quên mật khẩu đúng | gửi mã khớp hash đã lưu |
| 23 | nhập đúng mã reset | trả ticket, đánh dấu đã xác minh nhưng chưa dùng mã |
| 24 | nhập sai mã reset | `InvalidOrExpiredTokenException` |
| 25 | đặt lại mật khẩu bằng ticket đã xác minh | đổi mật khẩu, mã bị dùng, thu hồi mọi phiên |
| 26–28 | ticket chưa xác minh; đã dùng; hết hạn | `InvalidOrExpiredTokenException`, mật khẩu không đổi |
| 29 | đăng nhập GitHub với email mới | tạo tài khoản đã xác thực, không mật khẩu; lưu token GitHub đã mã hóa |
| 30 | đăng nhập GitHub, email trùng tài khoản đã xác thực | liên kết vào tài khoản đó, gửi email cảnh báo |
| 31 | đăng nhập GitHub, email trùng tài khoản chưa xác thực | `EmailNotVerifiedCannotLinkException`, không liên kết |
| 32 | GitHub không trả email | `GitHubEmailUnavailableException` |
| 33 | kết nối GitHub đã thuộc người khác | `GitHubAccountAlreadyLinkedException` |
| 34 | ngắt GitHub khi tài khoản không có mật khẩu | `GitHubDisconnectRequiresPasswordException` |
| 35 | ngắt GitHub khi đang là Manager của workspace có repo liên kết | `GitHubDisconnectBlockedByLinkedWorkspacesException` |
| 36 | ngắt GitHub hợp lệ | xóa liên kết và token đã lưu |

---

## Chưa được test
- **AI Gateway** (lớp gọi provider thật) và **chất lượng output của AI thật**. Phân tích DEFINE chỉ chạy AI thật vài lần khi test trên DB (xem `docs/lock-test-report.md` mục 7).
- **Workspace service** ngoài phần skill và lời mời: tạo/sửa/lưu trữ workspace, đổi role, xóa thành viên, liên kết repo GitHub.
- **Controller, DB thật và middleware lỗi** chưa có test tự động. Đã kiểm tra bằng tay qua HTTP trên DB dev (`docs/lock-test-report.md` mục 7).
