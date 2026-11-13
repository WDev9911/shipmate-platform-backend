# VERIFY: kế hoạch cài đặt chi tiết

Kế hoạch cài đặt VERIFY, viết trước khi code, cùng cách làm với `03-build.md`. Tên lớp, hàm và file trong tài liệu đã đối chiếu với code ngày 2026-11-13. Phần SHIP ở `05-ship.md`; hai tài liệu khớp nhau ở mục 9 (hợp đồng cho SHIP).

**Trạng thái cài đặt (2026-11-13): GĐ 1 đến 7 đã code xong, 828/828 test đạt.** Migration đã chạy trên DB dev (PostgreSQL 18.6) ngày 2026-11-13 và đã kiểm bằng truy vấn chỉ đọc: 12 snapshot có đúng 12 checklist với 60 mục (5 mục mỗi checklist, đủ năm key), version và workspace của checklist khớp snapshot, hai unique index và hai khóa chính đúng. GĐ 8 (chạy thật qua HTTP) đã chạy 25/25 đạt trên workspace thử, không tốn lượt AI hay GitHub; kết quả ở `docs/verify-test-report.md`. Đã chạy thật thêm: duyệt một challenge `AddDirectly` (1 lượt Gemini), version mới có checklist mới và version cũ giữ nguyên. Ca A của mục 11b (tiến độ feature, đếm drift và NBA `CompleteVerification`) đã chạy thật 8/8 đạt bằng dữ liệu chèn tay rồi xóa. Chưa chạy thật: Lock lần đầu qua HTTP (ca B), challenge loại `RemoveAndReplace` và `Backup` (ca C), và `info`/NBA `CompleteVerification` khi đã có submission được duyệt (cần GitHub); các ca này mới có unit test. Khác kế hoạch ở vài chỗ nhỏ:
- Hai cấu hình EF nằm chung một file `VerificationChecklistConfiguration.cs`.
- `SetApplicability` nói lại đúng điều đã đúng (đang áp dụng mà gửi `applicable: true`, hoặc đang bỏ qua mà gửi `false`) thì **không** bỏ tick và không ghi audit; bản kế hoạch chưa nêu trường hợp này.
- Test: 3 tệp mới (`VerificationChecklistTests` 15, `VerificationServiceTests` 23, `VerifyRequestValidatorTests` 7), 4 test mới ở `FeatureChallengeServiceTests`, 1 ở `LockServiceTests`, 6 ở `NextBestActionServiceTests` (tổng cộng thêm 56 test, nhiều hơn dự kiến 38 vì tách nhỏ hơn).
- Đã thêm mục 2.10 vào `docs/frontend-guide.md`.

**Bản gọn (2026-11-13).** Bản trước có thêm quy tắc "lỗi thời", kế thừa tick giữa các version và việc dọn `HasOpenDriftAsync`. Ba thứ đó được dời sang mục 15 ("Để sau"), kèm lý do. Quy tắc lỗi thời còn dựa trên một giả định sai: duyệt submission **không** đưa code vào `main` (xem mục 15).

Mục lục
1. Mục đích và phạm vi
2. Quyết định thiết kế (so với spec 2.4)
3. Bức tranh tổng thể
4. GĐ 1: Domain
5. GĐ 2: Hạ tầng (DB, repository, migration, dữ liệu cũ)
6. GĐ 3: Tạo checklist cùng snapshot (sửa LOCK)
7. GĐ 4: Service, DTO, validator, lỗi
8. GĐ 5: API
9. GĐ 6: Hợp đồng cho SHIP
10. GĐ 7: NBA
11. GĐ 8: Chạy thật qua HTTP
12. GĐ 9: Tài liệu
13. Danh sách test
14. Thứ tự commit gợi ý
15. Để sau (cố ý chưa làm)
16. Giới hạn đã biết

---

## 1. Mục đích và phạm vi

Trước khi một `snapshot_version` được SHIP, đội tự kiểm sản phẩm thật theo 5 mục cố định và ghi lại. Code VERIFY **không gọi AI, không gọi GitHub**, chỉ có logic và dữ liệu trong DB. Riêng việc **tạo dữ liệu để chạy thử** (Lock mới, challenge mới, submission đã duyệt) đi qua DEFINE, LOCK, BUILD nên có thể tốn lượt gọi AI; mục 11 ghi rõ bước nào.

Nói đúng mức: tick là **tự khai**. VERIFY không chứng minh sản phẩm chạy được; nó buộc đội dừng lại kiểm, ghi ai kiểm, lúc nào, ghi chú gì, và chặn SHIP khi chưa làm. Bằng chứng "đang chạy" là việc của SHIP (Ping, SSL).

Ngoài phạm vi: check tự động (spec bước 3 để ngỏ "nếu nhóm muốn mở rộng"), giao diện, CLI, sự kiện SignalR (frontend tải lại sau mỗi thao tác là đủ).

## 2. Quyết định thiết kế (so với spec 2.4)

| # | Spec 2.4 | Quyết định | Lý do |
|---|---|---|---|
| D1 | Checklist tạo khi Feature Challenge sinh snapshot mới | Tạo cả ở **Lock đầu** (version 1) và mỗi challenge được duyệt, **trong cùng lần lưu** với snapshot | Version 1 do Lock sinh ra; không có checklist thì version 1 không bao giờ ship được |
| D2 | Khóa chính `(snapshot_version, checklist_item)` | Bảng cha khóa `id`, **unique `mvp_snapshot_id`**; bảng mục khóa `(checklist_id, item_key)` | `MvpSnapshot.Version` là 1, 2, 3 **trong từng workspace**, số trần đụng nhau giữa các workspace. Gắn với id snapshot giữ đúng ý spec |
| D3 | Tự thích ứng (không có auth thì ẩn mục auth) | Luôn đủ 5 mục; **Manager** đánh dấu "không áp dụng" kèm **lý do bắt buộc**, ghi audit | Feature chỉ có tên và mô tả tự do; đoán bằng từ khóa dễ ẩn nhầm, ẩn nhầm là bỏ một bước kiểm mà không ai biết; gọi AI thì tốn lượt cho một luồng không cần AI |
| D4 | (spec BUILD) VERIFY bị chặn khi còn drift | Drift **chỉ hiển thị số lượng**, không chặn. Đếm cảnh báo mở của **các feature `Locked` trong version**, bất kể cảnh báo được tạo ở version nào | Spec SHIP nói drift tồn đọng chỉ trừ điểm, không chặn (tránh "Failure to Ship"); VERIFY chặn thì drift gián tiếp chặn SHIP. Đếm theo feature vì một cảnh báo tạo ở v1 vẫn mở khi challenge sinh v2, mà feature đó vẫn nằm trong v2; đếm theo `SnapshotVersionAtCheck` sẽ giấu nó (NBA cũng đếm theo feature như vậy) |
| D5 | Không có | Hiển thị **"đã duyệt x/y feature"**, không chặn | Đội biết version còn dở bao nhiêu. "Đã duyệt" nghĩa là submission mới nhất của feature ở trạng thái `Approved` (bot đã đẩy nhánh feature), **không** có nghĩa là đã vào `main` |
| D6 | Không có | **Ghi chú tùy chọn** mỗi lần tick (tối đa 500 ký tự) | Vẫn tự khai, nhưng để lại dấu vết cho người đọc sau |
| D7 | `all_items_checked` dùng chung cho SHIP và NBA | Lưu sẵn cột `all_items_checked`; SHIP và NBA đọc qua `GetReadinessAsync`, kèm danh sách mục còn thiếu | Spec SHIP bước 2 cần "hiển thị rõ mục nào còn thiếu", một `bool` không đủ |
| D8 | Spec: "Dev" tick | Mọi **thành viên đang hoạt động** tick; chỉ **Manager** đổi "áp dụng" | Ai trong đội cũng có thể kiểm; còn bỏ một mục kiểm là quyết định phạm vi, thuộc Manager |
| D9 | Không có | Migration **chèn sẵn checklist** (5 mục chưa tick) cho mọi snapshot đã có | Không Lock lại được một định nghĩa đã Lock (`ProductDefinitionLockedException`), nên workspace có trước VERIFY (kể cả workspace thử BUILD) sẽ không bao giờ có checklist nếu không chèn |

## 3. Bức tranh tổng thể

```
Lock v1 / duyệt challenge ──► snapshot mới ──► checklist mới (cùng lần lưu)

Thành viên tick ─┐                         verification_checklists.all_items_checked
Manager N/A  ────┴─► VerificationService ─►      (cập nhật trong lần ghi)
                            │
                            ├─► audit: VERIFY_ITEM_NOT_APPLICABLE, VERIFY_COMPLETED
                            ▼
              GetReadinessAsync(workspaceId, version)
                 │ allItemsChecked + missingItems
        ┌────────┴────────┐
        ▼                 ▼
      SHIP              NBA (CompleteVerification + verify_checklist_incomplete)
```

File mới và file sửa:

| Tầng | Mới | Sửa |
|---|---|---|
| Domain | `Constants/VerificationItemKeys.cs`, `Entities/VerificationChecklist.cs`, `Entities/VerificationChecklistItem.cs` | `Common/AuditTypes.cs` |
| Application | `DTOs/Verify/*.cs`, `Validators/Verify/*.cs`, `Exceptions/VerifyExceptions.cs`, `Interfaces/Repositories/IVerificationChecklistRepository.cs`, `Interfaces/Services/IVerificationService.cs`, `Services/VerificationService.cs` | `DependencyInjection.cs`, `LockService.cs`, `FeatureChallengeService.cs`, `NextBestActionService.cs`, `DTOs/Build/NextBestActionDto.cs` |
| Infrastructure | `Configurations/VerificationChecklistConfiguration.cs`, `Configurations/VerificationChecklistItemConfiguration.cs`, `Repositories/VerificationChecklistRepository.cs`, migration `AddVerificationChecklists` | `AppDbContext.cs`, `DependencyInjection.cs` |
| API | `Controllers/VerifyController.cs` | — |
| Tests | `Domain/VerificationChecklistTests.cs`, `Application/VerificationServiceTests.cs`, `Application/VerifyRequestValidatorTests.cs` | `LockServiceTests.cs`, `FeatureChallengeServiceTests.cs`, `NextBestActionServiceTests.cs`, `Scenarios/LockWorld.cs` |

---

## 4. GĐ 1: Domain

### 4.1. `Constants/VerificationItemKeys.cs`

Chuỗi cố định thay cho enum: lưu thẳng vào DB, trả thẳng ra API, cùng cách `NextBestActionBlockedReasons`.

```csharp
public static class VerificationItemKeys
{
    public const string MainUserJourney = "main_user_journey";   // hành trình chính từ đầu đến cuối
    public const string Authentication = "authentication";      // đăng nhập, đăng xuất, phân quyền
    public const string ProductionConfig = "production_config"; // biến môi trường, secrets production
    public const string BasicCrud = "basic_crud";               // tạo, đọc, sửa, xóa dữ liệu chính
    public const string ErrorHandling = "error_handling";       // lỗi không làm trắng trang

    // The order the list is shown in.
    public static readonly IReadOnlyList<string> All =
        [MainUserJourney, Authentication, ProductionConfig, BasicCrud, ErrorHandling];

    public static bool IsKnown(string key) => All.Contains(key);
}
```

### 4.2. `Entities/VerificationChecklistItem.cs`

Không kế thừa `BaseEntity` (khóa ghép, không cần `Id`).

```csharp
public class VerificationChecklistItem
{
    public Guid ChecklistId { get; set; }
    public string ItemKey { get; set; } = string.Empty;

    public bool Applicable { get; set; } = true;
    public string? NotApplicableReason { get; set; }

    public bool Checked { get; set; }
    public string? Note { get; set; }
    public Guid? CheckedByUserId { get; set; }
    public DateTime? CheckedAt { get; set; }
}
```

### 4.3. `Entities/VerificationChecklist.cs`

Mọi thay đổi đi qua phương thức của entity, theo cách `DriftAlert` làm (trả kết quả; service đổi thành lỗi HTTP). `AllItemsChecked` và `CompletedAt` chỉ được tính ở một chỗ (`Recompute`).

Key lạ được service chặn **trước** khi gọi entity (lỗi 400), nên entity coi key luôn hợp lệ và không kiểm lại.

```csharp
public enum ChecklistEditError { None, ItemNotApplicable, MainJourneyRequired }

// Did this edit finish the list? The service writes VERIFY_COMPLETED only then.
public enum ChecklistTransition { None, Completed, Reopened }

public record ChecklistEditResult(ChecklistEditError Error, ChecklistTransition Transition)
{
    public static ChecklistEditResult Fail(ChecklistEditError error) => new(error, ChecklistTransition.None);
}

public class VerificationChecklist : BaseEntity
{
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    public Guid MvpSnapshotId { get; set; }
    public MvpSnapshot MvpSnapshot { get; set; } = null!;
    public int SnapshotVersion { get; set; }

    public bool AllItemsChecked { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Bumped on every write, so two people ticking at once can't leave AllItemsChecked wrong.
    public int Revision { get; private set; }

    public List<VerificationChecklistItem> Items { get; set; } = new();

    // Sets WorkspaceId, MvpSnapshotId and SnapshotVersion from the snapshot (foreign key only, not the navigation).
    public static VerificationChecklist CreateFor(MvpSnapshot snapshot) { /* 5 mục, Applicable = true, Checked = false */ }

    public ChecklistEditResult Check(string key, bool isChecked, string? note, Guid userId, DateTime now);
    public ChecklistEditResult SetApplicability(string key, bool applicable, string? reason, DateTime now);

    // Items still to tick, in the fixed order.
    public IReadOnlyList<string> MissingItems();

    private ChecklistTransition Recompute(DateTime now);
}
```

Quy tắc trong từng phương thức:

| Phương thức | Từ chối khi | Làm |
|---|---|---|
| `Check(key, true, ...)` | Mục không áp dụng (`ItemNotApplicable`) | `Checked = true`, `Note`, `CheckedByUserId`, `CheckedAt`; rồi `Recompute` |
| `Check(key, false, ...)` | Không bao giờ (bỏ tick mục không áp dụng là việc không làm gì) | `Checked = false`, xóa `CheckedByUserId` và `CheckedAt`, cập nhật `Note` nếu có gửi; rồi `Recompute` |
| `SetApplicability(key, false, reason)` | `key` là `main_user_journey` (`MainJourneyRequired`) | `Applicable = false`, lưu lý do, `Checked = false`; rồi `Recompute` |
| `SetApplicability(key, true, _)` | Không bao giờ | `Applicable = true`, xóa lý do, `Checked = false` (phải kiểm lại); rồi `Recompute` |
| `Recompute` | — | `all = mọi mục Applicable đều Checked`. `false → true`: `CompletedAt = now`, trả `Completed`. `true → false`: `CompletedAt = null`, trả `Reopened`. Luôn `UpdatedAt = now`, `Revision++` |

Lý do bắt buộc khi `applicable = false` kiểm ở validator (mục 7.3), không lặp ở entity.

### 4.4. `Common/AuditTypes.cs`

```csharp
// Verification
public const string VerifyItemNotApplicable = "VERIFY_ITEM_NOT_APPLICABLE";
public const string VerifyCompleted = "VERIFY_COMPLETED";
```

**Xong GĐ 1 khi:** test `VerificationChecklistTests` (mục 13.1) đạt; chưa đụng DB.

---

## 5. GĐ 2: Hạ tầng

### 5.1. `VerificationChecklistConfiguration.cs`

Theo mẫu `DriftAlertConfiguration` (tên cột snake_case viết tay):

| Thuộc tính | Cột | Ghi chú |
|---|---|---|
| bảng | `verification_checklists` | |
| `Id` | `id` | khóa |
| `WorkspaceId` | `workspace_id` | FK `workspaces`, xóa theo |
| `MvpSnapshotId` | `mvp_snapshot_id` | FK `mvp_snapshots`, xóa theo (giống `snapshot_features`), **unique**. PostgreSQL cho phép hai đường xóa theo (từ workspace và từ snapshot) |
| `SnapshotVersion` | `snapshot_version` | **unique cùng `workspace_id`** (truy vấn theo version) |
| `AllItemsChecked` | `all_items_checked` | |
| `CompletedAt`, `UpdatedAt`, `CreatedAt` | `completed_at`, `updated_at`, `created_at` | |
| `Revision` | `revision` | `.IsConcurrencyToken()` |
| `Items` | | `HasMany(...).WithOne().HasForeignKey(i => i.ChecklistId).OnDelete(Cascade)` |

EF Core ghi được thuộc tính `private set` mà không cần cấu hình thêm.

### 5.2. `VerificationChecklistItemConfiguration.cs`

| Thuộc tính | Cột | Ghi chú |
|---|---|---|
| bảng | `verification_checklist_items` | |
| `(ChecklistId, ItemKey)` | `checklist_id`, `item_key` | khóa ghép; `item_key` tối đa 40 |
| `Applicable` | `applicable` | |
| `NotApplicableReason` | `not_applicable_reason` | tối đa 500 |
| `Checked` | `checked` | |
| `Note` | `note` | tối đa 500 |
| `CheckedByUserId`, `CheckedAt` | `checked_by_user_id`, `checked_at` | |

### 5.3. `AppDbContext.cs`

- Thêm `DbSet<VerificationChecklist> VerificationChecklists` và `DbSet<VerificationChecklistItem> VerificationChecklistItems`.
- Trong `SaveChangesAsync`, cạnh nhánh `Workspace` đang có:

```csharp
catch (DbUpdateConcurrencyException ex) when (ex.Entries.Any(e => e.Entity is VerificationChecklist))
{
    throw new VerificationChecklistConflictException();
}
```

### 5.4. Repository

```csharp
public interface IVerificationChecklistRepository
{
    Task AddAsync(VerificationChecklist checklist);

    // With its items and its snapshot's features (for "x/y features approved").
    Task<VerificationChecklist?> GetByVersionAsync(Guid workspaceId, int snapshotVersion);

    // Newest version first, items included (for missing counts), no snapshot.
    Task<List<VerificationChecklist>> GetByWorkspaceIdAsync(Guid workspaceId);

    Task<bool> SaveChangesAsync();
}
```

`GetByVersionAsync`: `.Include(c => c.Items).Include(c => c.MvpSnapshot).ThenInclude(s => s.Features)`, có theo dõi (để sửa).

Không thêm hàm vào repository có sẵn. Số drift (D4) dùng `IDriftAlertRepository.GetOpenByWorkspaceAsync` đã có (NBA đang dùng), lọc theo các `FeatureId` `Locked` của snapshot. Không đụng `HasOpenDriftAsync` (xem mục 15).

Đăng ký `IVerificationChecklistRepository` trong `ShipMate.Infrastructure/DependencyInjection.cs`.

### 5.5. Migration và dữ liệu cũ (D9)

```bash
dotnet ef migrations add AddVerificationChecklists --project ShipMate.Infrastructure --startup-project ShipMate.API
```

Sau phần tạo bảng EF sinh ra, thêm vào cuối `Up` của migration:

```csharp
// Snapshots locked before VERIFY existed get an empty list too: a locked definition can't be locked again.
migrationBuilder.Sql("""
    INSERT INTO verification_checklists
        (id, workspace_id, mvp_snapshot_id, snapshot_version, all_items_checked, completed_at, created_at, updated_at, revision)
    SELECT md5(s.id::text || clock_timestamp()::text)::uuid, s.workspace_id, s.id, s.version, false, NULL, now(), NULL, 0
    FROM mvp_snapshots s
    WHERE NOT EXISTS (SELECT 1 FROM verification_checklists c WHERE c.mvp_snapshot_id = s.id);

    INSERT INTO verification_checklist_items (checklist_id, item_key, applicable, checked)
    SELECT c.id, k.item_key, true, false
    FROM verification_checklists c
    CROSS JOIN (VALUES ('main_user_journey'), ('authentication'), ('production_config'),
                       ('basic_crud'), ('error_handling')) AS k(item_key)
    WHERE NOT EXISTS (SELECT 1 FROM verification_checklist_items i WHERE i.checklist_id = c.id);
    """);
```

`Down` chỉ cần xóa hai bảng (EF sinh sẵn).

Id sinh bằng `md5(...)::uuid` thay vì `gen_random_uuid()`: `gen_random_uuid()` chỉ có sẵn từ PostgreSQL 13, mà phiên bản PostgreSQL của DB dev chưa kiểm được. `md5` cho ra 32 ký tự hex, ép được sang `uuid` ở mọi phiên bản. Id này chỉ cần duy nhất, không cần đúng chuẩn UUID v4.

```bash
dotnet ef database update --project ShipMate.Infrastructure --startup-project ShipMate.API
```

Đọc lại file migration trước khi chạy: hai bảng, unique `mvp_snapshot_id`, unique `(workspace_id, snapshot_version)`, khóa ghép bảng mục, xóa theo từ checklist và từ workspace, đoạn SQL chèn sẵn.

**Xong GĐ 2 khi:** `dotnet build` đạt; migration chạy được trên DB dev; mọi snapshot đã có (gồm của các workspace thử BUILD còn trong DB) có đúng một checklist với 5 mục.

---

## 6. GĐ 3: Tạo checklist cùng snapshot (sửa LOCK)

Cả hai nơi thêm checklist **trước** lần `SaveChangesAsync` đang có, nên snapshot, bản ghi audit và checklist cùng được lưu hoặc cùng không. Các repository dùng chung một `AppDbContext` (đăng ký Scoped), nên `AddAsync` ở repository này được lưu bởi `SaveChangesAsync` của repository kia.

### 6.1. `LockService.LockAsync`

Thêm `IVerificationChecklistRepository` vào constructor. Sau `await _productDefinitionRepository.AddSnapshotAsync(snapshot);`:

```csharp
// VERIFY needs a list for every version, the first one included.
await _verificationChecklistRepository.AddAsync(VerificationChecklist.CreateFor(snapshot));
```

### 6.2. `FeatureChallengeService.ApproveAsync`

Thêm `IVerificationChecklistRepository` vào constructor. Sau `await _productDefinitionRepository.AddSnapshotAsync(next);`:

```csharp
// Every new version is checked again from scratch.
await _verificationChecklistRepository.AddAsync(VerificationChecklist.CreateFor(next));
```

Không cần xử lý đua tranh riêng: `FeatureChallengeRepository.SaveChangesAsync` đã dùng `SaveGuardingSnapshotVersionAsync`, nên hai lần duyệt cùng lúc thì một bên nhận `SnapshotVersionConflictException` và checklist của nó cũng không được lưu. Khi cổng `LockGatePolicy` không qua thì hàm đã ném lỗi trước đoạn này, nên không có checklist mồ côi.

### 6.3. Sửa test đang có vì constructor đổi

`LockServiceTests.cs`, `FeatureChallengeServiceTests.cs`, `Scenarios/LockWorld.cs`: thêm `Substitute.For<IVerificationChecklistRepository>()` vào chỗ `new LockService(...)` và `new FeatureChallengeService(...)`.

**Xong GĐ 3 khi:** toàn bộ test cũ vẫn đạt (hiện 772) cộng test mới ở mục 13.2.

---

## 7. GĐ 4: Service, DTO, validator, lỗi

### 7.1. DTO (`DTOs/Verify/`)

```csharp
public class CheckVerificationItemRequest { public bool Checked { get; set; } public string? Note { get; set; } }
public class SetVerificationItemApplicabilityRequest { public bool Applicable { get; set; } public string? Reason { get; set; } }

public class VerificationItemDto
{
    public string Key { get; set; } = string.Empty;
    public bool Applicable { get; set; }
    public string? NotApplicableReason { get; set; }
    public bool Checked { get; set; }
    public string? Note { get; set; }
    public Guid? CheckedByUserId { get; set; }
    public DateTime? CheckedAt { get; set; }
}

public class VerificationInfoDto { public int OpenDriftCount { get; set; } public int ApprovedFeatures { get; set; } public int LockedFeatures { get; set; } }

public class VerificationChecklistDto
{
    public int Version { get; set; }
    public bool AllItemsChecked { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<string> MissingItems { get; set; } = new();
    public List<VerificationItemDto> Items { get; set; } = new();
    public VerificationInfoDto Info { get; set; } = new();
}

public class VerificationChecklistSummaryDto
{
    public int Version { get; set; }
    public bool IsLatest { get; set; }
    public bool AllItemsChecked { get; set; }
    public int MissingCount { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public record VerificationReadiness(
    bool Exists, bool AllItemsChecked, IReadOnlyList<string> MissingItems, DateTime? CompletedAt, Guid? ChecklistId)
{
    public static readonly VerificationReadiness Missing = new(false, false, [], null, null);
}
```

Map bằng tay trong service (như `DriftAlertService.ToDto`), không thêm profile AutoMapper.

### 7.2. Lỗi (`Exceptions/VerifyExceptions.cs`)

| Lớp | `ErrorCode` | HTTP | Khi nào |
|---|---|---|---|
| `VerificationItemUnknownException` | `VERIFY_ITEM_UNKNOWN` | 400 | `itemKey` trên route không thuộc 5 key |
| `VerificationItemNotApplicableException` | `VERIFY_ITEM_NOT_APPLICABLE` | 409 | Tick mục đang không áp dụng |
| `VerificationMainJourneyRequiredException` | `VERIFY_MAIN_JOURNEY_REQUIRED` | 409 | Đánh dấu `main_user_journey` không áp dụng |
| `VerificationChecklistConflictException` | `VERIFY_CHECKLIST_CONFLICT` | 409 | Hai người sửa cùng lúc; frontend tải lại rồi làm lại |

Không tìm thấy version: `NotFoundException("Verification checklist", version)` (mã chung `NOT_FOUND`, như các luồng khác). Người ngoài workspace: guard trả `NOT_FOUND`. Thành viên không phải Manager đổi "áp dụng": guard trả `WorkspacePermissionDeniedException`.

### 7.3. Validator (`Validators/Verify/`)

Không gọi validator bằng tay: `ValidationFilter` (đăng ký trong `Program.cs`) tự chạy validator của mọi tham số body trước khi vào controller, và trả `400` theo dạng chung.

```csharp
public class CheckVerificationItemRequestValidator : AbstractValidator<CheckVerificationItemRequest>
{
    public CheckVerificationItemRequestValidator() =>
        RuleFor(x => x.Note).MaximumLength(500);
}

public class SetVerificationItemApplicabilityRequestValidator : AbstractValidator<SetVerificationItemApplicabilityRequest>
{
    public SetVerificationItemApplicabilityRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MinimumLength(10).MaximumLength(500)
            .When(x => !x.Applicable)
            .WithMessage("Give a reason (10 to 500 characters) for skipping this check.");
    }
}
```

### 7.4. `IVerificationService` và `VerificationService`

```csharp
public interface IVerificationService
{
    Task<List<VerificationChecklistSummaryDto>> GetAllAsync(Guid userId, Guid workspaceId);
    Task<VerificationChecklistDto> GetAsync(Guid userId, Guid workspaceId, int version);
    Task<VerificationChecklistDto> CheckItemAsync(Guid userId, Guid workspaceId, int version, string itemKey, CheckVerificationItemRequest request);
    Task<VerificationChecklistDto> SetApplicabilityAsync(Guid userId, Guid workspaceId, int version, string itemKey, SetVerificationItemApplicabilityRequest request);

    // No access check: SHIP and NBA call this after checking who is asking.
    Task<VerificationReadiness> GetReadinessAsync(Guid workspaceId, int version);
}
```

Phụ thuộc: `IWorkspaceAccessGuard`, `IVerificationChecklistRepository`, `IBuildSubmissionRepository`, `IDriftAlertRepository`, `IAuditTrail`.

Luồng từng hàm:

| Hàm | Bước |
|---|---|
| `GetAllAsync` | guard thành viên → lấy danh sách → map summary; `IsLatest` cho version lớn nhất |
| `GetAsync` | guard thành viên → `GetByVersionAsync` hoặc 404 → `ToDtoAsync` |
| `CheckItemAsync` | guard thành viên → `IsKnown(itemKey)` không thì 400 → lấy checklist hoặc 404 → `checklist.Check(...)` → đổi lỗi thành exception → nếu `Completed` thì `AppendAsync(VERIFY_COMPLETED)` → `SaveChangesAsync` → `ToDtoAsync` |
| `SetApplicabilityAsync` | guard **Manager** → `IsKnown` → lấy checklist → `SetApplicability(...)` → `AppendAsync(VERIFY_ITEM_NOT_APPLICABLE)` (cả khi bật lại; `applicable` cho biết chiều) → nếu `Completed` thì thêm `VERIFY_COMPLETED` → lưu → DTO |
| `GetReadinessAsync` | `GetByVersionAsync`; không có thì `VerificationReadiness.Missing`; có thì trả từ cột đã lưu và `MissingItems()`. **Không** đếm drift hay feature. (`GetByVersionAsync` có nạp kèm feature của snapshot, thừa cho hàm này; với MVP vài feature thì chấp nhận được, không thêm hàm repository riêng) |

`ToDtoAsync(checklist)`:
- `AllItemsChecked`, `CompletedAt` lấy thẳng từ cột đã lưu; `MissingItems` từ `checklist.MissingItems()`.
- `Info.OpenDriftCount` = số cảnh báo trong `await _alerts.GetOpenByWorkspaceAsync(ws)` có `FeatureId` thuộc các feature `Locked` của snapshot (D4).
- `Info.LockedFeatures` = số feature `Locked` của snapshot; `Info.ApprovedFeatures` = số feature trong đó có `GetLatestAsync(ws, featureId)?.Status == Approved` (một truy vấn mỗi feature, giống NBA; MVP có 3 đến 10 feature).
- Mục sắp theo thứ tự `VerificationItemKeys.All`.

Nói thật về chi phí đọc: `all_items_checked` đọc thẳng từ cột như spec muốn; riêng phần `Info` (drift, tiến độ) được tính mỗi lần mở chi tiết. `GetReadinessAsync` (SHIP và NBA dùng) không tính phần đó.

Payload audit:

```json
// VERIFY_ITEM_NOT_APPLICABLE
{ "version": 2, "itemKey": "authentication", "applicable": false, "reason": "Ứng dụng không có tài khoản" }

// VERIFY_COMPLETED
{ "version": 2, "checklistId": "…",
  "items": [ { "key": "authentication", "applicable": false, "checked": false }, … ] }
```

Đăng ký `IVerificationService` trong `ShipMate.Application/DependencyInjection.cs`.

---

## 8. GĐ 5: API

`API/Controllers/VerifyController.cs`, theo mẫu `BuildController` (`[ApiController]`, `[Authorize]`, `ICurrentUserService`).

| Method | Route (`api/workspaces/{workspaceId:guid}/verify/...`) | Ai | Body | Trả |
|---|---|---|---|---|
| GET | `checklists` | Thành viên | — | `200` danh sách summary |
| GET | `checklists/{version:int}` | Thành viên | — | `200` chi tiết |
| PUT | `checklists/{version:int}/items/{itemKey}` | Thành viên | `{ "checked": true, "note": "…" }` | `200` chi tiết |
| PUT | `checklists/{version:int}/items/{itemKey}/applicability` | Manager | `{ "applicable": false, "reason": "…" }` | `200` chi tiết |

Ví dụ phản hồi chi tiết:

```json
{
  "version": 2,
  "allItemsChecked": false,
  "completedAt": null,
  "missingItems": ["basic_crud"],
  "items": [
    { "key": "main_user_journey", "applicable": true, "notApplicableReason": null,
      "checked": true, "note": "Đặt lịch → xác nhận → xem lịch, trên bản deploy", "checkedByUserId": "…", "checkedAt": "…" },
    { "key": "authentication", "applicable": false, "notApplicableReason": "Ứng dụng không có tài khoản người dùng",
      "checked": false, "note": null, "checkedByUserId": null, "checkedAt": null }
  ],
  "info": { "openDriftCount": 1, "approvedFeatures": 3, "lockedFeatures": 4 }
}
```

Workspace đã Archive: guard hiện **không** kiểm `Archived` ở luồng nào, nên VERIFY cũng không thêm (giữ nhất quán; ghi vào giới hạn).

---

## 9. GĐ 6: Hợp đồng cho SHIP

SHIP **không** đọc bảng VERIFY; chỉ gọi:

```csharp
Task<VerificationReadiness> IVerificationService.GetReadinessAsync(Guid workspaceId, int version);
```

| Trường | SHIP dùng để |
|---|---|
| `Exists = false` | Chặn: version này không có checklist |
| `AllItemsChecked = false` | Chặn cứng (spec SHIP bước 2) |
| `MissingItems` | Hiển thị mục còn thiếu |
| `ChecklistId`, `CompletedAt` | Lưu vào Ship Card |

VERIFY không cung cấp hàm đếm drift cho SHIP: spec SHIP đếm drift theo `SnapshotVersionAtCheck` (khác cách đếm theo feature của VERIFY, D4), nên SHIP tự thêm hàm đếm của mình (`05-ship.md` mục 8).

---

## 10. GĐ 7: NBA

### 10.1. `NextBestActionDto.cs`

```csharp
public enum NextBestActionType
{
    FixDrift, ApproveSubmission, WorkOnFeature, ClaimFeature, Blocked,
    // Every feature of the version is approved but its Verification Checklist isn't done: shipping waits on it.
    CompleteVerification,
    NoAction
}

public static class NextBestActionBlockedReasons
{
    public const string DependencyPendingAssignment = "dependency_pending_assignment";
    public const string DependencyDriftUnresolved = "dependency_drift_unresolved";
    // Shipping this version waits for its Verification Checklist.
    public const string VerifyChecklistIncomplete = "verify_checklist_incomplete";
}

// trong NextBestActionDto
// CompleteVerification: the version whose checklist is waiting.
public int? SnapshotVersion { get; set; }
```

API trả enum dưới dạng chuỗi (`JsonStringEnumConverter` trong `Program.cs`), nên chèn `CompleteVerification` vào giữa không đổi gì phía frontend.

**Báo frontend:** `BlockedReason` từ nay xuất hiện ở **hai** loại hành động: `Blocked` (kèm `BlockedByFeatureId`, như cũ) và `CompleteVerification` (kèm `SnapshotVersion`, không có `BlockedByFeatureId`).

### 10.2. `NextBestActionService.GetAsync`

Thêm `IVerificationService` vào constructor. Bước 6, ngay trước `return new NextBestActionDto { Action = NoAction }`:

```csharp
// 6. Nothing left to build in this version but its checklist isn't done: VERIFY is next, and SHIP waits for it.
if (await EveryLockedFeatureApprovedAsync(workspaceId, snapshot)
    && !(await _verificationService.GetReadinessAsync(workspaceId, snapshot.Version)).AllItemsChecked)
{
    return new NextBestActionDto
    {
        Action = NextBestActionType.CompleteVerification,
        BlockedReason = NextBestActionBlockedReasons.VerifyChecklistIncomplete,
        SnapshotVersion = snapshot.Version
    };
}
```

`EveryLockedFeatureApprovedAsync`: snapshot có ít nhất một feature `Locked`, và mọi feature đó có `GetLatestAsync(...)?.Status == Approved`.

Bước 1 đến 5 không đổi, nên NBA **không bao giờ** che việc code bằng gợi ý VERIFY.

`NextBestActionServiceTests.cs`: thêm `Substitute.For<IVerificationService>()` vào constructor, **và** đặt mặc định trong constructor của lớp test:

```csharp
_verification.GetReadinessAsync(default, default).ReturnsForAnyArgs(
    new VerificationReadiness(true, true, [], DateTime.UtcNow, Guid.NewGuid()));
```

Lý do: test có sẵn `ApprovedCode_IsDoneUntilYouSubmitMore` có một feature duy nhất đã `Approved` và mong `NoAction`, nên chạm đúng bước 6. Không đặt mặc định thì NSubstitute trả `null` cho `Task<VerificationReadiness>` (record không phải lớp ảo) và test ném `NullReferenceException`; đặt mặc định "chưa xong" thì test đổi sang `CompleteVerification`. Mặc định "đã xong" giữ nguyên mọi test cũ; test mới ở mục 13.5 tự đặt "chưa xong".

---

## 11. GĐ 8: Chạy thật qua HTTP

Trên DB dev, API chạy local. Các API của VERIFY không gọi AI hay GitHub, nhưng **dữ liệu để thử** thì có thể cần:

| Cần | Đi qua | Tốn |
|---|---|---|
| Một workspace Lock mới (bước 2) | Lock đòi một lần phân tích DEFINE thành công (`HasSuccessfulAnalysisRunAsync`) | Lượt gọi AI của DEFINE, trừ khi DB dev còn workspace đã `ReadyForLock` mà chưa Lock |
| Một challenge được duyệt (bước 12) | Gửi challenge chấm độ phức tạp bằng AI (`ComplexityScoringClient.ScoreAsync`) | **1 lượt gọi Gemini**, trừ khi DB dev còn challenge đang `Open` |
| Submission `Approved` (bước 15) | Duyệt qua BUILD cần GitHub App thật | GitHub; hoặc chèn tạm một dòng `build_submissions` trạng thái `Approved` rồi xóa sau khi thử |

Mọi lượt gọi AI đều hỏi bạn trước, như ở BUILD. Bước nào không có dữ liệu sẵn mà bạn không muốn tốn lượt gọi thì bỏ qua: chúng đã có unit test ở mục 13.2 và 13.5; ghi "chưa chạy thật" vào báo cáo.

| # | Làm | Kỳ vọng |
|---|---|---|
| 1 | Sau migration, xem các workspace có sẵn (gồm workspace thử BUILD) | Mọi snapshot đều có đúng một checklist, 5 mục chưa tick |
| 2 | Lock một workspace (xem bảng trên) | `GET .../verify/checklists` có version 1, `allItemsChecked: false`, `missingItems` đủ 5 |
| 3 | Dev tick `main_user_journey` kèm ghi chú | `200`, mục có `checkedByUserId`, `checkedAt`, `note` |
| 4 | Dev đánh dấu `authentication` không áp dụng | `403` `WORKSPACE_PERMISSION_DENIED` |
| 5 | Manager đánh dấu không áp dụng, lý do 5 ký tự | `400` validation |
| 6 | Manager đánh dấu `main_user_journey` không áp dụng | `409` `VERIFY_MAIN_JOURNEY_REQUIRED` |
| 7 | Manager đánh dấu `authentication` không áp dụng, lý do đủ | `200`; audit có `VERIFY_ITEM_NOT_APPLICABLE` |
| 8 | Tick `authentication` | `409` `VERIFY_ITEM_NOT_APPLICABLE` |
| 9 | Bỏ tick `authentication` | `200`, không đổi gì |
| 10 | Tick 3 mục còn lại | `allItemsChecked: true`, `completedAt` có giá trị; audit có `VERIFY_COMPLETED` |
| 11 | Bỏ tick một mục | `allItemsChecked: false`, `completedAt: null` |
| 12 | Duyệt một challenge (`AddDirectly` hoặc `Backup`; xem bảng trên) | Version mới có checklist mới, 5 mục chưa tick; checklist version cũ giữ nguyên |
| 13 | Hai request tick cùng lúc (hai lệnh `curl` song song) | Một `200`; cái còn lại `200` hoặc `409 VERIFY_CHECKLIST_CONFLICT`; `all_items_checked` trong DB khớp trạng thái các mục |
| 14 | Người ngoài workspace gọi `GET` | `404 NOT_FOUND` |
| 15 | Workspace có mọi feature `Locked` với submission mới nhất `Approved` (xem bảng trên): `GET .../next-best-action` khi checklist chưa xong | `action: "CompleteVerification"`, `blockedReason: "verify_checklist_incomplete"`; `info.approvedFeatures` bằng `info.lockedFeatures` |
| 16 | Kiểm chuỗi audit sau tất cả | `FindFirstBrokenAsync` trả `null` |

Ghi kết quả vào `docs/verify-test-report.md` theo dạng `build-test-report.md`.

### 11b. GĐ 8b: ba ca còn lại (kế hoạch, 2026-11-13)

Sau GĐ 8 còn ba ca chưa chạy thật. Dữ liệu DB dev đã đọc (chỉ đọc) để lên kế hoạch:

| Workspace | Tình trạng | Dùng cho |
|---|---|---|
| "Race test - Lock" (`03b506bc…`) | Đã Lock, v1, 4 feature khóa đều độ phức tạp 1, chưa ai nhận, chưa có submission; chỉ có Manager thử (kỹ năng 5); ngân sách 10 | Ca A và ca C |
| "Phòng khám Nha Khoa - Đặt lịch" (`9fa36fa6…`) | Workspace **duy nhất** chưa Lock; là workspace demo DEFINE (`define-demo-data.md`), 7 feature chưa chấm điểm | **Không dùng**: Lock là một chiều, sẽ đóng băng dữ liệu demo |
| "LOCK test - Nha Khoa" (`ad75067c…`) | Có feature khóa độ phức tạp 5 trong khi kỹ năng cao nhất là 4, nên cổng chặn mọi lần duyệt; không thể làm Backup ở đây | Không dùng |

**Đánh giá thật về giá trị.** Lần chạy GĐ 8 đã duyệt một challenge `AddDirectly` qua HTTP: snapshot mới và checklist của nó được EF lưu chung một lần (chèn snapshot trước, checklist sau) và đúng. Lock lần đầu và challenge `RemoveAndReplace`, `Backup` đi qua **đúng dòng code đó** (`VerificationChecklist.CreateFor` ngay sau `AddSnapshotAsync`, cùng lần lưu). Nên ca B và ca C thêm ít bằng chứng mới. Ca A thì khác: phần đọc tiến độ, drift và bước 6 của NBA **chưa chạy thật lần nào**.

| Ca | Kiểm điều gì | Tốn | Đề xuất |
|---|---|---|---|
| A. `info` khác 0 và NBA `CompleteVerification` | Truy vấn tiến độ feature, đếm drift theo feature, bước 6 của NBA trên DB thật | 0 AI, 0 GitHub; **ghi tạm vào DB** rồi xóa | **Nên làm** |
| B. Lock lần đầu qua HTTP | `LockService` tạo checklist v1 | **1 lượt Gemini** (phân tích DEFINE cho một workspace mới) | Tùy chọn |
| C. Challenge `RemoveAndReplace` và `Backup` | Hai loại duyệt còn lại tạo checklist mới; checklist không đếm feature dự phòng | **2 lượt Gemini** (mỗi challenge chấm điểm một lần) | Tùy chọn |

#### Ca A: tiến độ, drift và NBA (0 AI)

Chỉ dữ liệu thử **ghi thẳng vào DB** mới tạo được submission `Approved` mà không cần GitHub. Mọi dòng chèn đều có dấu để xóa chính xác: `staging_branch = 'verify-test'`, `branch_name` bắt đầu bằng `feature/verify-test-`. Chèn và xóa chạy trong giao dịch bằng công cụ đọc/ghi DB ở scratchpad (ngoài repo), không in chuỗi kết nối.

Chuẩn bị (một giao dịch), trên "Race test - Lock", version 1, cho cả 4 feature khóa:
- `feature_assignments`: giao feature cho Manager thử (`branch_name = 'feature/verify-test-<n>'`). Cần, vì không giao thì NBA dừng ở bước 4 (gợi ý nhận feature) trước khi tới bước 6.
- `build_submissions`: một submission mỗi feature, `status = 'approved'`, `approved_at = now()`, `snapshot_version = 1`, `submitted_by_user_id` = Manager, `staging_branch = 'verify-test'`, `head_sha` 40 ký tự giả, `suspected_feature_ids = '{}'`.

| # | Làm | Kỳ vọng |
|---|---|---|
| A1 | `GET .../verify/checklists/1` | `info`: `approvedFeatures 4`, `lockedFeatures 4`, `openDriftCount 0` |
| A2 | `GET .../next-best-action` (Manager) | `action: "CompleteVerification"`, `blockedReason: "verify_checklist_incomplete"`, `snapshotVersion: 1`, không có `blockedByFeatureId` |
| A3 | Chèn một `drift_alerts` `unresolved` cho feature thứ nhất (trỏ vào submission vừa chèn), rồi `GET .../verify/checklists/1` | `openDriftCount 1`; `approvedFeatures` vẫn 4 (submission mới nhất vẫn `Approved`); checklist **không** bị chặn |
| A4 | `GET .../next-best-action` | `FixDrift` cho feature đó: drift của chính mình được ưu tiên hơn VERIFY |
| A5 | Xóa cảnh báo vừa chèn; tick đủ 5 mục checklist v1; gọi lại NBA | `allItemsChecked: true`; NBA trả `NoAction` |
| A6 | Bỏ tick một mục; gọi lại NBA | Lại `CompleteVerification` |
| A7 | Dọn: xóa cảnh báo, submission, assignment đã chèn (lọc theo dấu); kiểm lại số dòng và chuỗi audit | Số dòng về như trước; chuỗi audit nguyên vẹn |

Còn lại sau ca A: checklist v1 của "Race test - Lock" có tick, và các bản ghi audit `VERIFY_*` (chuỗi chỉ thêm, không xóa được). Không có bản ghi audit nào cho dữ liệu chèn tay, vì chúng không đi qua service.

Rủi ro: cần đúng tên cột và giá trị trạng thái (`approved`, `unresolved` dạng snake_case như cấu hình EF). Đã đọc các cột bắt buộc của ba bảng; nếu một lệnh chèn sai thì cả giao dịch hủy, không để lại dữ liệu dở.

#### Ca B: Lock lần đầu qua HTTP (1 lượt Gemini)

Tạo **workspace mới** chứ không Lock workspace demo DEFINE.

| # | Làm | Tốn | Kỳ vọng |
|---|---|---|---|
| B1 | Manager thử tạo workspace (tên và vision prompt ngắn, 2 đến 3 feature) | 0 | `201` |
| B2 | Chạy phân tích DEFINE | **1 lượt Gemini** | Có `features`, lần chạy `succeeded` |
| B3 | Đi qua các bước để định nghĩa sẵn sàng Lock (`ProductDefinitionReadinessPolicy`; đọc lại luật này lúc làm, có thể cần duyệt từng feature) | 0 | `ReadyForLock` |
| B4 | Manager khai kỹ năng (`PUT .../members/me/skill`), đặt độ phức tạp **bằng tay** cho từng feature (`PUT .../product-definition/complexity`, không gọi AI) sao cho tổng ≤ 10 | 0 | Cổng qua |
| B5 | `POST .../lock` | 0 | `version 1`; `GET .../verify/checklists` có đúng v1, 5 mục chưa tick |
| B6 | Kiểm DB: checklist gắn snapshot v1 theo id; có `SNAPSHOT_LOCKED`; chuỗi audit nguyên vẹn | 0 | Đạt |

Rủi ro: nếu luật "sẵn sàng Lock" đòi một bước dùng AI khác (ví dụ tạo lại feature) thì dừng và hỏi trước, không tự gọi thêm.

#### Ca C: challenge `RemoveAndReplace` và `Backup` (2 lượt Gemini)

Trên "Race test - Lock", **sau** ca A (ca A cần đúng 4 feature của v1).

| # | Làm | Tốn | Kỳ vọng |
|---|---|---|---|
| C1 | Manager gửi challenge nhỏ; xác nhận độ phức tạp 1; duyệt `RemoveAndReplace` bỏ một feature không có phụ thuộc | **1 lượt Gemini** | v2 có checklist mới chưa tick; feature bị bỏ là `replaced`; checklist v1 giữ nguyên |
| C2 | Manager hạ **tạm** kỹ năng của mình xuống 4; gửi challenge; xác nhận độ phức tạp 5; duyệt `Backup`; trả kỹ năng về 5 | **1 lượt Gemini** | v3 có checklist mới chưa tick; `info.lockedFeatures` **không** đếm feature dự phòng (vẫn 4) |
| C3 | Kiểm DB và chuỗi audit | 0 | 3 checklist khớp 3 snapshot; chuỗi nguyên vẹn; kỹ năng Manager về 5 |

Vì sao phải hạ kỹ năng ở C2: `Backup` chỉ được khi độ phức tạp **lớn hơn** kỹ năng cao nhất (`BackupNotApplicableException`). Thang điểm tối đa là 5 mà Manager đang 5, nên phải hạ xuống 4 trong lúc duyệt. Cổng không chặn vì feature dự phòng được miễn trần kỹ năng, và tổng 4 + 5 = 9 vẫn trong ngân sách 10.

**Tổng nếu làm cả ba ca: 3 lượt Gemini, 0 GitHub.** Thứ tự: A, rồi C, rồi B (B độc lập).

---

## 12. GĐ 9: Tài liệu

- `docs/frontend-guide.md`: mục mới cho VERIFY (4 endpoint, ví dụ phản hồi, mã lỗi, cách hiển thị `missingItems` và `info`, hành động NBA mới và `BlockedReason` ở hai loại hành động).
- `docs/flows/04-verify.md` (file này): sửa từng mục thành "đã cài đặt" khi xong, như `02-lock.md`.
- `docs/verify-test-report.md`: báo cáo.

---

## 13. Danh sách test

### 13.1. `Domain/VerificationChecklistTests.cs`
- `CreateFor_HasTheFiveItems_AllApplicableAndUnchecked`
- `Check_TheLastApplicableItem_CompletesTheList_AndSetsCompletedAt`
- `Uncheck_AfterCompletion_ReopensIt_AndClearsCompletedAt`
- `Check_SameValueTwice_ReportsNoTransition`
- `Check_ANotApplicableItem_IsRejected`
- `Uncheck_ANotApplicableItem_DoesNothing`
- `Uncheck_ClearsWhoAndWhen_ButKeepsTheNote`
- `MarkNotApplicable_TheLastUncheckedItem_CompletesTheList`
- `MarkNotApplicable_MainUserJourney_IsRejected`
- `MarkApplicableAgain_UnchecksIt_AndReopens`
- `MissingItems_SkipsNotApplicable_AndKeepsTheFixedOrder`
- `EveryWrite_BumpsTheRevision`

### 13.2. LOCK (`LockServiceTests.cs`, `FeatureChallengeServiceTests.cs`)
- `Lock_AddsTheVersion1Checklist`
- `Approve_AddsAFreshChecklistForTheNewVersion`
- `Approve_WhenTheGateFails_AddsNoChecklist`

### 13.3. `Application/VerificationServiceTests.cs`
- `GetAll_ListsNewestFirst_AndFlagsTheLatest`
- `Get_UnknownVersion_IsNotFound`
- `Get_ShowsOpenDriftAndFeatureProgress`
- `Get_CountsADriftRaisedInAnEarlierVersion_WhenItsFeatureIsStillLocked`
- `Get_DoesNotCountADriftOnAReplacedFeature`
- `CheckItem_ByAMember_RecordsWhoAndWhen`
- `CheckItem_UnknownKey_IsBadRequest`
- `CheckItem_CompletingTheList_AppendsVerifyCompleted`
- `CheckItem_NotCompleting_AppendsNothing`
- `SetApplicability_ByADeveloper_IsDenied`
- `SetApplicability_ByTheManager_AppendsItsRecord`
- `SetApplicability_ThatCompletesTheList_AppendsBothRecords`
- `GetReadiness_NoChecklist_IsMissing`
- `GetReadiness_ItemsMissing_ListsThem`
- `GetReadiness_Complete_HasNoMissingItems`

### 13.4. `Application/VerifyRequestValidatorTests.cs`
- `NotApplicable_WithoutAReason_IsInvalid`
- `NotApplicable_WithAShortReason_IsInvalid`
- `Applicable_WithoutAReason_IsValid`
- `Note_LongerThan500_IsInvalid`

### 13.5. `Application/NextBestActionServiceTests.cs`
- `EverythingApproved_AndChecklistNotDone_SuggestsCompleteVerification`
- `EverythingApproved_AndChecklistDone_ReturnsNoAction`
- `OwnWorkStillComesFirst_BeforeVerification`
- `NoLockedFeatures_DoesNotSuggestVerification`

Ước lượng: khoảng 38 test mới.

---

## 14. Thứ tự commit gợi ý

Mỗi commit tự build và test đạt:

1. `feat: add the verification checklist model and its rules` (GĐ 1 + test 13.1)
2. `feat: store verification checklists and give existing snapshots one` (GĐ 2, migration)
3. `feat: create a verification checklist with every new snapshot` (GĐ 3 + test 13.2)
4. `feat: add the VERIFY API` (GĐ 4, 5, 6 + test 13.3, 13.4)
5. `feat: point NBA at an unfinished verification checklist` (GĐ 7 + test 13.5)
6. `docs: VERIFY flow, frontend guide and test report` (GĐ 8, 9)

---

## 15. Để sau (cố ý chưa làm)

| Ý | Vì sao chưa làm | Khi nào làm, và làm thế nào |
|---|---|---|
| **Checklist "lỗi thời"** khi code đổi sau lúc kiểm | Bản trước định dùng "có submission được duyệt sau lúc hoàn tất". Sai: duyệt chỉ khiến bot đẩy **nhánh feature** (`BuildSubmissionStatus.Approved`); code vào `main` khi đội merge PR trên GitHub, ShipMate không ghi lúc đó. Quy tắc đó vừa báo oan (duyệt mà chưa merge) vừa bỏ sót (duyệt trước, merge sau) | Khi cần thật: lưu SHA của `main` lúc hoàn tất (gọi GitHub), so với SHA lúc SHIP. SHIP vẫn gọi URL production lúc ship nên lớp bảo vệ chính không mất |
| **Kế thừa tick** khi challenge chỉ thêm feature `backup` | Hiếm gặp; tốn một hàm, một quy tắc và vài test | Khi đội thấy phiền vì phải kiểm lại cả 5 mục cho một version không có code mới |
| **Xóa `HasOpenDriftAsync`** | Nó được làm cho việc VERIFY chặn khi còn drift, mà giờ VERIFY không chặn. Nhưng đây là code BUILD | Một commit riêng sau VERIFY; sửa luôn `03-build.md`, `03-build-chua-lam.md`, `build-test-report.md` |

---

## 16. Giới hạn đã biết

- **Tick là tự khai.** VERIFY không chứng minh sản phẩm chạy.
- **Không biết code đã đổi sau lúc kiểm** (mục 15). Đội tự bỏ tick khi thấy cần.
- **5 mục thiên về web app.** Sản phẩm không giao diện dùng "không áp dụng".
- **"Đã duyệt x/y feature"** đếm theo trạng thái `Approved` của submission, không phải theo việc đã vào `main`.
- **Nhận feature `backup` đổi trạng thái trong cùng version**, không sinh checklist mới; con số `lockedFeatures` tự tăng theo.
- **Workspace Archive vẫn sửa được**, giống mọi luồng khác hiện tại.
- **Đổi constructor** của `LockService`, `FeatureChallengeService`, `NextBestActionService` làm hỏng biên dịch test cũ cho tới khi GĐ 3 và GĐ 7 sửa xong chỗ khởi tạo.
