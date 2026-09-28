# Luồng 1 (Define) và Demo

## 2.1. DEFINE — Định nghĩa sản phẩm (Bảo)

**Mục đích:** Biến một ý tưởng mơ hồ, chưa có cấu trúc thành một bản đặc tả MVP rõ ràng, có ranh giới, với feature đã được định hình cụ thể — làm nền cho việc chấm điểm phức tạp ở bước LOCK.

**Phân quyền [CẬP NHẬT].** Trong toàn bộ luồng này, "Dev" là **Manager của workspace**. Chỉ Manager được nhập/sửa dữ liệu, chạy AI, xử lý cảnh báo và chuyển Product Definition sang `READY_FOR_LOCK`. Các thành viên Developer khác của workspace chỉ được xem.

## Diễn giải từng bước

### Bước 1 — Nhập liệu

Dev nhập "Product Vision Prompt" (text tự do, bắt buộc) — mô tả ý tưởng sản phẩm. Nội dung có thể là ý tưởng của Dev, hoặc yêu cầu khách hàng được Dev tường thuật lại. **[CẬP NHẬT]** Vision Prompt chính là field `VisionPrompt` của workspace: nhập khi tạo workspace, và sửa được ngay trong DEFINE. Không có bản Vision Prompt thứ hai nào khác.

Ngoài ra, nếu có, Dev có thể nhập thêm "Feature đã chốt với khách" (danh sách có cấu trúc riêng, tên feature + mô tả ngắn) — chỉ áp dụng khi Dev và khách hàng đã thống nhất trước 1 số feature ngoài phạm vi hệ thống. Nếu không có, bỏ qua phần này, luồng vẫn chạy bình thường chỉ với Vision Prompt.

### Bước 2 — AI phân tích

Hệ thống gửi Vision Prompt cho AI (AI Ideation & Feature Extractor), kèm danh sách "Feature đã chốt với khách" nếu Dev có nhập. AI làm 4 việc theo đúng thứ tự, chỉ áp dụng cho phần Vision Prompt tự do:

- **2.1. Xác định 1 persona chính duy nhất (primary persona).** Đồng thời AI xác định các **vai trò phụ (supporting roles)** — những người dùng khác mà hệ thống bắt buộc phải có để hành trình của persona chính chạy được. Ví dụ: bác sĩ phải tạo lịch khám thì bệnh nhân mới xem được lịch. Vai trò phụ **không phải persona ngang hàng**: không dùng để đánh giá feature "cần hay không cần", chỉ dùng để phát hiện chức năng hỗ trợ bắt buộc.
- **2.2. Trích ra các feature MVP cốt lõi** — chỉ những gì persona chính bắt buộc cần. Không giới hạn cứng số lượng. Mỗi feature được gắn nhãn `role`:
    - `primary` — feature persona chính dùng trực tiếp.
    - `supporting` — feature của vai trò phụ, bắt buộc phải có để ít nhất 1 feature `primary` hoạt động (enabling feature). AI chỉ được đề xuất feature `supporting` khi chỉ ra được feature `primary` nào cần nó; feature `primary` đó phải có feature `supporting` trong `depends_on`, kèm lý do. Feature `supporting` **không được đưa vào Kill List với lý do "không phục vụ persona chính"**.
- **2.3. Tự sinh Kill List** — với mỗi feature bị đề xuất loại, AI gắn nhãn nguồn gốc (xem Quy tắc nguồn gốc ở Bước 6):
    - `from_ai` — AI tự nghĩ ra, không ai nhắc tới trong prompt → loại tự do, không cần xác nhận. Dev vẫn có quyền cứu feature này lại nếu muốn.
    - `from_customer_mentioned` — có nhắc tới trong Vision Prompt nhưng chưa rõ đã "chốt" → gắn `status: pending_confirmation`, không tính là loại thật cho đến khi Dev xác nhận riêng.
- **2.4. Xác định quan hệ phụ thuộc** logic giữa các feature cốt lõi (ví dụ: Feature B cần dữ liệu/luồng từ Feature A để chạy). AI tự động đề xuất mảng `depends_on` cho từng feature, dựa trên hành trình Persona (Persona User Journey) — ví dụ: "Đăng nhập" phải xong trước "Xem hồ sơ cá nhân". Bao gồm cả phụ thuộc từ feature `primary` sang feature `supporting` (ví dụ: "Xem lịch bác sĩ" phụ thuộc "Bác sĩ tạo lịch khám").

### Bước 3 — Kiểm tra chéo

Trước khi tổng hợp output, nếu có "Feature đã chốt với khách", AI thực hiện 2 bước kiểm tra chéo:

- **Dedupe check** — gắn `possible_duplicate: true` lên các feature nếu phát hiện feature trích từ Vision Prompt và feature đã chốt mô tả cùng 1 chức năng, kèm giải thích ngắn và `duplicate_of: <feature_id>` để chỉ rõ cặp nào bị nghi trùng.
- **Persona conflict check** — gắn `persona_conflict: true` lên feature đã chốt nếu nó không phù hợp logic với persona AI vừa xác định, kèm giải thích ngắn. AI không tự đổi persona hay tự loại feature để "khớp" lại — chỉ cảnh báo. Feature đã chốt phục vụ một vai trò phụ đã được xác định ở Bước 2.1 thì không tính là xung đột.

### Bước 4 — Output

Output trả về cho dev gồm 2 phần tách riêng, cùng lấy từ 1 bộ dữ liệu Product Definition:

- **(a) Danh sách chức năng (Functional List)** — các feature đã định hình đầy đủ (tên, mô tả, phạm vi, `role`, `origin`, `sources`, đánh giá của AI, trạng thái), kèm cờ `possible_duplicate`/`persona_conflict` nếu có, và danh sách `depends_on` do AI đề xuất.
- **(b) Báo cáo (Report)** — persona đã chọn kèm lý do, danh sách vai trò phụ kèm lý do, problem-solution pair, tóm tắt Kill List, tổng hợp các cảnh báo (nếu có).

### Bước 5 — Dev review

Dev xem lại Product Definition. Mỗi feature đã có sẵn trạng thái mặc định theo Bảng trạng thái mặc định (Bước 7); Dev chỉ bắt buộc xử lý những feature đang `pending_confirmation` và các cảnh báo.

- Với Kill List có `status: pending_confirmation` → Dev bấm "Đồng ý loại" hoặc "Không, giữ lại". Nếu chọn "Không, giữ lại", feature quay lại feature list và giữ nguyên `origin` theo đúng nguồn đã có sẵn từ đầu — không đổi origin, chỉ đổi trạng thái từ "bị đề xuất loại" về "giữ lại".
- Với feature có `possible_duplicate: true` → Dev chọn "Gộp lại thành 1 feature" hoặc "Giữ riêng, đây là 2 feature khác nhau". Nếu gộp, feature gộp nhận `sources` là hợp của 2 bên, và `origin` được tính lại theo thứ tự ưu tiên ở Bước 6. Nếu 1 trong 2 feature là `from_committed_list`, **nội dung (tên, mô tả, phạm vi) của feature đã chốt được giữ nguyên**; muốn đưa nội dung của feature kia vào thì phải đi qua luồng "Yêu cầu thay đổi feature đã chốt" (Bước 8). **[CẬP NHẬT]** Nếu không bên nào là `from_committed_list`, nội dung mặc định lấy từ feature có `origin` ưu tiên cao hơn; nếu 2 bên bằng nhau thì lấy feature đứng trước trong danh sách. Sau khi gộp, Dev sửa tay được nội dung theo quy tắc sửa tay bên dưới.
- Với feature có `persona_conflict: true` → Dev chọn "Giữ nguyên, đây là ngoại lệ hợp lý" hoặc "Cần sửa lại persona" (yêu cầu AI đánh giá lại persona dựa trên toàn bộ feature hiện có — chạy lại AI theo Bước 5b).
- Dev xem lại danh sách phụ thuộc do AI gợi ý trên giao diện Functional List, có thể bấm thêm/xóa liên kết phụ thuộc giữa các feature trước khi chốt sang bước LOCK. Nếu Dev loại một feature mà feature `included` khác đang phụ thuộc vào, hệ thống cảnh báo và yêu cầu Dev chọn một trong hai: xóa liên kết phụ thuộc, hoặc loại luôn feature phụ thuộc.
- **[CẬP NHẬT] Sửa tay:** Dev được sửa trực tiếp tên, mô tả, phạm vi của feature `from_ai` và `from_customer_mentioned`, không cần xác nhận với khách hàng. Feature `from_committed_list` vẫn phải đi qua Bước 8.
- **[CẬP NHẬT] Không thêm feature bằng tay:** muốn bổ sung feature, Dev sửa Vision Prompt, hoặc thêm vào "Feature đã chốt với khách" (nếu đã thống nhất với khách), rồi chạy lại AI (Bước 5b). Nhờ vậy mọi feature đều có 1 trong 3 `origin` ở Bước 6 và truy ngược được nguồn gốc.
- Dev có thể yêu cầu AI điều chỉnh feature `from_ai` (VD: đổi persona), nhưng chưa bị khóa — đây là giai đoạn "đề xuất", chưa "cam kết". **[CẬP NHẬT]** Việc điều chỉnh này là chạy lại AI theo Bước 5b.

### Bước 5b — Chạy lại AI [CẬP NHẬT]

Chạy lại AI xảy ra khi Dev yêu cầu AI điều chỉnh, khi Dev chọn "Cần sửa lại persona", hoặc sau khi Dev sửa Vision Prompt / danh sách "Feature đã chốt với khách". Nguyên tắc: **không làm mất quyết định Dev đã đưa ra.**

- Mỗi feature có cờ `dev_decided` (mặc định `false`). Cờ này được bật khi Dev:
    - đổi trạng thái feature (đồng ý loại, giữ lại, cứu lại, INCLUDE/EXCLUDE),
    - gộp feature, hoặc chọn "Giữ riêng" khi xử lý `possible_duplicate`,
    - chọn "Giữ nguyên" khi xử lý `persona_conflict`,
    - sửa tay nội dung feature,
    - thêm/xóa liên kết `depends_on` (bật cờ cho cả 2 feature ở 2 đầu liên kết).
- Khi chạy lại, **giữ nguyên**: mọi feature có `dev_decided = true`, và mọi feature `from_customer_mentioned` / `from_committed_list` (vì 2 loại này đến từ input, không phải do AI tự nghĩ ra).
- AI **chỉ được sinh lại** các feature `from_ai` có `dev_decided = false` (thêm mới, sửa, hoặc bỏ).
- Hệ thống gửi kèm toàn bộ trạng thái hiện tại cho AI để AI biết phần nào phải giữ.
- AI được đánh giá lại `ai_assessment` của mọi feature (nhất là khi persona đổi), nhưng **không được đổi `status`** của feature có `dev_decided = true`.
- Sau khi chạy lại, liên kết `depends_on` nào trỏ tới feature không còn tồn tại sẽ bị xóa, và hệ thống cảnh báo cho Dev.
- Nếu persona đổi, `locked_persona` được cập nhật. Trong DEFINE, persona vẫn còn đổi được; chỉ cố định từ khi LOCK (Bước 9).

### Bước 6 — Quy tắc nguồn gốc (origin & sources)

Có 3 giá trị nguồn gốc:

- `from_ai` — AI tự đề xuất, không có trong Vision Prompt.
- `from_customer_mentioned` — có nhắc tới trong Vision Prompt.
- `from_committed_list` — Dev nhập ở mục "Feature đã chốt với khách".

Mỗi feature có 2 field:

- `sources[]` — mảng ghi **tất cả** các nguồn mà feature từng gắn với (ví dụ sau khi gộp). Chỉ dùng để truy vết và audit.
- `origin` — **một giá trị duy nhất**, là nguồn có độ ưu tiên cao nhất trong `sources`, theo thứ tự: `from_committed_list` > `from_customer_mentioned` > `from_ai`. **Mọi quy tắc quyết định (trạng thái mặc định, quyền sửa, quyền loại) chỉ đọc theo `origin`.**

Việc AI có thấy feature phù hợp hay không **không phải là một nguồn**, mà được ghi ở field riêng `ai_assessment` (`required` / `not_required` + lý do). Ví dụ: khách nhắc tới "Chat với bác sĩ" và AI thấy phù hợp → `origin: from_customer_mentioned`, `ai_assessment: required`.

### Bước 7 — Bảng trạng thái mặc định

Trạng thái feature chỉ có 3 giá trị: `included`, `excluded`, `pending_confirmation`. Kill List là danh sách các feature đang `excluded` hoặc đang `pending_confirmation` do AI đề xuất loại.

| origin | ai_assessment | Trạng thái mặc định | Dev muốn đổi trạng thái |
| --- | --- | --- | --- |
| `from_ai` | `required` | `included` | Tự do |
| `from_ai` | `not_required` | `excluded` (vào Kill List) | Tự do (cứu lại) |
| `from_customer_mentioned` | bất kỳ | `pending_confirmation` | Dev bắt buộc chọn INCLUDE / EXCLUDE |
| `from_committed_list` | bất kỳ | `pending_confirmation` | INCLUDE: tự do. EXCLUDE: phải qua xác nhận khách hàng (Bước 8) |

Product Definition chỉ được chuyển sang trạng thái `READY_FOR_LOCK` khi:

- không còn feature nào `pending_confirmation`,
- mọi cờ `possible_duplicate` và `persona_conflict` đã được Dev xử lý,
- đồ thị `depends_on` không có vòng lặp.

### Bước 8 — Thay đổi feature đã chốt (`from_committed_list`)

Dev không được sửa tay trực tiếp, **và cũng không được loại (EXCLUDE) trực tiếp**. Muốn sửa hoặc loại, Dev bấm "Yêu cầu thay đổi feature đã chốt" → nhập lý do (bắt buộc) → tick xác nhận "Tôi xác nhận đã/sẽ thông báo lại với khách hàng về thay đổi này" → hệ thống mới cho thực hiện, và ghi vào audit trail (loại thay đổi, nội dung cũ, nội dung mới, lý do, thời điểm, người thực hiện). INCLUDE lại một feature đã chốt từng bị loại không cần xác nhận, nhưng vẫn được ghi vào audit trail. Cơ chế này tách biệt với Feature Challenge của LOCK.

### Bước 9 — Sau khi LOCK [CẬP NHẬT] *(cần người phụ trách LOCK xác nhận)*

Sau lần LOCK đầu tiên thành công, Product Definition bị **đóng băng**: không sửa feature, không chạy lại AI, không đổi trạng thái trong DEFINE. `VisionPrompt` của workspace cũng không sửa được nữa, vì đó là input của DEFINE. Mọi thay đổi phạm vi sau thời điểm này phải đi qua Feature Challenge của LOCK.

---

## Điểm kỹ thuật cần lưu ý

- AI phải trả structured output (JSON). Tách 2 schema:
    - **Input:** `visionPrompt` (bắt buộc), `committedFeatures[]` (optional). **[CẬP NHẬT]** Khi chạy lại (Bước 5b) gửi thêm `existingFeatures[]` và `currentPersona` để AI biết phần nào phải giữ.
    - **Output:** `persona`, `features[]`. Mỗi item trong `features[]` có `id`, `name`, `description`, `scope`, `role` (`primary` | `supporting`), `origin`, `sources[]`, `ai_assessment` (`verdict`, `reason`), `status`, `possible_duplicate`, `duplicate_of`, `persona_conflict`, `depends_on: []` (danh sách feature_id tiền đề).
    - **Không có mảng `killList[]` riêng.** Kill List được lọc ra từ `features[]` theo `status`, để feature được cứu lại luôn còn đủ thông tin.
- **[CẬP NHẬT]** Mỗi feature lưu thêm field hệ thống `dev_decided` (bool). Field này không do AI trả về.
- Bảng lưu feature đã chốt cần thêm `change_history[]`, mỗi bản ghi gồm: `action` (`edit` | `exclude` | `include` | `merge`), nội dung cũ, nội dung mới, lý do, `customer_notified_confirmed`, người thực hiện, timestamp.
- Không đặt giới hạn cứng số lượng feature — chỉ hiển thị khuyến nghị mềm: "Sản phẩm MVP hiệu quả thường có 3-5 feature cốt lõi. Bạn hiện có [X] feature — hãy cân nhắc xem có feature nào nên đưa vào Kill List không." Chỉ đếm feature `primary`; feature `supporting` hiển thị riêng, không tính vào con số khuyến nghị.
- (a) Functional List và (b) Report là 2 view xuất từ cùng 1 bộ dữ liệu, không sinh 2 lần riêng biệt.
- Khi feature được "cứu" từ Kill List hoặc gộp từ duplicate, `origin` phải được gán rõ theo đúng quy tắc ở **Bước 6** — không để mặc định ngẫu nhiên.
- Persona đã được AI xác định (Bước 2.1) phải được lưu lại như 1 field cố định trong Product Definition (`locked_persona`), không chỉ tồn tại tạm thời trong phiên làm việc. Đây là dữ liệu nền để Persona Conflict Check có thể được các module sau (LOCK) gọi lại và so khớp, không phải chỉ chạy 1 lần duy nhất tại DEFINE. `locked_persona` gồm: persona chính, lý do, và danh sách vai trò phụ. **[CẬP NHẬT]** Trong DEFINE, `locked_persona` vẫn đổi được qua chạy lại AI (Bước 5b); chỉ cố định từ khi LOCK (Bước 9).
- **[CẬP NHẬT]** Mỗi lần gọi AI, lưu một bản chụp (snapshot) Vision Prompt đã gửi, kèm thời điểm và người chạy, vào Product Definition để truy vết AI đã phân tích dựa trên prompt nào.
- **[CẬP NHẬT]** AI được gọi qua một lớp trung gian chung (AI Gateway): gửi prompt + JSON schema, nhận về JSON. Chọn provider nào (Claude, OpenAI, Gemini...) tùy theo credit nhóm đang có; đổi provider sau này không ảnh hưởng luồng.

---

## Tóm tắt thay đổi vòng này

1. **Chạy lại AI:** thêm Bước 5b và cờ `dev_decided`. Quyết định của Dev được giữ nguyên, AI chỉ sinh lại các feature `from_ai` Dev chưa đụng tới.
2. **Thêm feature bằng tay:** không cho phép. Muốn thêm thì bổ sung input rồi chạy lại AI, để giữ nguyên 3 `origin`.
3. **Sửa tay:** cho sửa tự do feature `from_ai` và `from_customer_mentioned`. Feature đã chốt vẫn theo Bước 8.
4. **Gộp khi không có feature đã chốt:** nội dung mặc định lấy theo `origin` ưu tiên cao hơn, bằng nhau thì lấy feature đứng trước, sau đó Dev sửa được.
5. **Phân quyền:** chỉ Manager của workspace thao tác, Developer chỉ xem.
6. **Vision Prompt:** dùng chung `VisionPrompt` của workspace, mỗi lần chạy AI lưu snapshot.
7. **Sau LOCK:** DEFINE đóng băng, mọi thay đổi đi qua Feature Challenge (Bước 9).
8. **AI Gateway:** gọi AI qua lớp trung gian, provider chọn sau.

## Quyết định khi triển khai (M0)

1. **Bổ sung field cho output của AI**, vì spec yêu cầu các thông tin này nhưng schema chưa có chỗ lưu:
    - `problem_solution` (`problem`, `solution`) — cho phần problem-solution pair của Report.
    - `duplicate_reason` — giải thích ngắn đi kèm `possible_duplicate`.
    - `persona_conflict_reason` — giải thích ngắn đi kèm `persona_conflict`.
    - `depends_on` đổi thành dạng `[{ feature_id, reason }]` — để lưu lý do phụ thuộc (bắt buộc với phụ thuộc `primary` → `supporting`).
2. **Feature đã chốt nhập sai trước lần chạy AI đầu tiên được sửa/xóa tự do.** Lúc đó nó vẫn đang là input, chưa đi vào Product Definition. Từ sau lần chạy AI đầu tiên mới áp dụng Bước 8.

## Điểm còn để ngỏ

- Bước 9 cần người phụ trách LOCK xác nhận.
- Spec chưa nói Product Definition đang ở `READY_FOR_LOCK` mà Dev sửa tiếp thì xử lý thế nào. Gợi ý: kiểm tra lại 3 điều kiện, nếu không còn thỏa thì quay về trạng thái nháp. Phần này cần nhóm chốt.
