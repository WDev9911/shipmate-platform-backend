# Báo cáo Unit Test — luồng DEFINE

**Kết quả: 117 test, 117 pass, 0 fail.** Chạy bằng `dotnet test ShipMate.Tests`. Không dùng DB, không gọi AI (repository được giả lập bằng NSubstitute).

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

## Chưa được test
- Phần gọi AI (`ProductDefinitionAnalysisService`, AI Gateway), theo yêu cầu.
- Auth (đăng nhập sai mật khẩu, OTP...) và Workspace, vì không thuộc luồng DEFINE.
- Controller, truy vấn DB thật, và middleware chuyển exception thành JSON trả về client.
