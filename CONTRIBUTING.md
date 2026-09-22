# Contributing Guide

Hướng dẫn quy trình làm việc và quy ước code cho team ShipMate.

## 1. Git Workflow

- `main` luôn ở trạng thái chạy được (deployable) — **không push thẳng lên `main`**.
- Mỗi feature/fix làm trên 1 branch riêng, tạo từ `main`:

```bash
git checkout main
git pull origin main
git checkout -b feature/<ten-tinh-nang>
```

**Quy ước đặt tên branch:**

| Prefix | Dùng khi |
|---|---|
| `feature/...` | Thêm tính năng mới |
| `fix/...` | Sửa lỗi |
| `refactor/...` | Tái cấu trúc code, không đổi behavior |
| `docs/...` | Chỉ thay đổi tài liệu |

Ví dụ: `feature/order-management`, `fix/order-status-validation`.

**Commit message** theo [Conventional Commits](https://www.conventionalcommits.org/):

```
feat: thêm API tạo order
fix: sửa lỗi validate địa chỉ giao hàng
refactor: tách IOrderRepository ra khỏi Common
docs: cập nhật README phần cài đặt
```

**Push & mở Pull Request:**

```bash
git push -u origin feature/<ten-tinh-nang>
```

Sau đó mở PR vào `main` trên GitHub, dùng template có sẵn (`.github/PULL_REQUEST_TEMPLATE.md`).

- PR cần ít nhất **1 người review** trước khi merge (team 2 backend nên review chéo lẫn nhau).
- Merge bằng **Squash and merge** để lịch sử `main` gọn gàng.
- Xóa branch sau khi merge.

## 2. Quy ước kiến trúc & vị trí đặt code

Dự án theo Clean Architecture, dependency chỉ trỏ vào trong (`API → Application → Domain`, `Infrastructure → Domain/Application`). Khi thêm 1 entity/feature mới, đặt file theo bảng sau:

| Loại | Vị trí |
|---|---|
| Entity | `Domain/Entities/` |
| Enum | `Domain/Enums/` |
| Exception vi phạm business rule (throw từ entity) | `Domain/Exceptions/` |
| DTO (request/response) | `Application/DTOs/<TenFeature>/` |
| Interface repository | `Application/Interfaces/Repositories/` |
| Interface service | `Application/Interfaces/Services/` |
| Implementation service | `Application/Services/` |
| AutoMapper profile | `Application/Mappings/` |
| FluentValidation validator | `Application/Validators/<TenFeature>/` |
| Exception điều phối use case (NotFound, Forbidden...) | `Application/Exceptions/` |
| Implementation repository (EF Core) | `Infrastructure/Persistence/Repositories/` |
| EF Core entity configuration | `Infrastructure/Persistence/Configurations/` |
| Controller | `API/Controllers/` |

**Nguyên tắc quan trọng:** Interface luôn nằm ở `Application` (nơi tiêu thụ nó), implementation kỹ thuật (EF Core...) nằm ở `Infrastructure` — Infrastructure implement theo hợp đồng do Application định nghĩa (Dependency Inversion), không phải ngược lại.

## 3. Quy trình thêm 1 feature mới (end-to-end)

1. Tạo entity ở `Domain/Entities/`.
2. Tạo DTO request/response ở `Application/DTOs/`.
3. Tạo interface repository ở `Application/Interfaces/Repositories/`, implement ở `Infrastructure/Persistence/Repositories/`.
4. Tạo interface service ở `Application/Interfaces/Services/`, implement ở `Application/Services/`.
5. Tạo AutoMapper profile ở `Application/Mappings/` để map Entity ↔ DTO.
6. Tạo validator ở `Application/Validators/` cho request DTO.
7. Đăng ký DI: `AddScoped<IXxxRepository, XxxRepository>()` trong `Infrastructure/DependencyInjection.cs`, `AddScoped<IXxxService, XxxService>()` trong `Application/DependencyInjection.cs`.
8. Tạo controller ở `API/Controllers/`, inject interface service (không inject thẳng implementation).
9. Lỗi nghiệp vụ throw `NotFoundException`/exception phù hợp — `GlobalExceptionHandler` sẽ tự map sang HTTP status + response dạng ProblemDetails, không cần try/catch thủ công trong controller.

## 4. Coding convention

- Class, method, property: `PascalCase`. Local variable, parameter: `camelCase`.
- Method bất đồng bộ luôn có hậu tố `Async` (`GetByIdAsync`).
- Interface luôn có tiền tố `I` (`IOrderService`).
- Không comment mô tả code làm gì (code phải tự giải thích qua tên gọi) — chỉ comment khi có lý do ngầm (workaround, giới hạn kỹ thuật...).

## 5. Trước khi mở PR

- [ ] `dotnet build` không lỗi, không warning mới
- [ ] Đã test thủ công qua Swagger (hoặc test tự động nếu có)
- [ ] Không commit file nhạy cảm (connection string thật, secret...)
- [ ] PR mô tả rõ thay đổi, theo đúng template
