# ShipMate

**An AI-First Product Governance and Scope Discipline Platform for Rapid MVP Execution**

ShipMate là web platform đóng vai trò **lớp quản trị sản phẩm (Product Governance Layer)**, chạy phía trên Git repo của dev. ShipMate không sửa code trực tiếp — nó giám sát tiến độ, chặn scope creep, và ra quyết định hộ dev thông qua khung 5 giai đoạn **"Path to Ship"**.

## Bối cảnh

Công cụ AI code (Cursor, v0, Bolt.new, Replit Agent...) khiến bottleneck của phát triển sản phẩm chuyển từ "khả năng code" sang "kỷ luật sản phẩm & kiểm soát scope". Không có lớp quản trị, dev dễ rơi vào:

- **Scope Creep** — liên tục yêu cầu AI thêm tính năng ngoài lề
- **Feature Bloat** — nhầm "AI làm được" với "nên làm"
- **Decision Overload** — backlog phình to, không biết việc gì ưu tiên
- **Failure to Ship** — code 90% xong nhưng không bao giờ deploy thật

Khoảng cách "Idea → Code" gần như bằng 0, nhưng "Code → Shipped Product" ngày càng xa. Các tool PM hiện có (Jira/Trello/Notion) chỉ là kho task passive, đo năng suất bằng số ticket đóng chứ không phải time-to-market.

## Path to Ship — 5 giai đoạn

| Giai đoạn | Mục tiêu |
|---|---|
| **DEFINE** | Xác định rõ vấn đề, đối tượng người dùng, phạm vi MVP |
| **LOCK** | Chốt scope, khóa lại để tránh feature creep giữa chừng |
| **BUILD** | Thực thi code trong phạm vi đã lock |
| **VERIFY** | Kiểm tra chất lượng, đối chiếu lại với scope đã định |
| **SHIP** | Deploy thật, đưa sản phẩm ra người dùng |

## Kiến trúc

Backend theo mô hình **Clean Architecture** (.NET 8), dependency chỉ trỏ vào trong:

```
API  →  Application  →  Domain
 └────────→ Infrastructure ──┘
```

| Layer | Vai trò |
|---|---|
| `ShipMate.Domain` | Entities, Enums, Exceptions nghiệp vụ — không phụ thuộc layer nào khác |
| `ShipMate.Application` | Use case, DTO, Interface (Repository/Service), Validator, Mapping |
| `ShipMate.Infrastructure` | EF Core, implement repository, các service kỹ thuật |
| `ShipMate.API` | Controller, middleware, cấu hình DI |

**Tech stack:** .NET 8 · ASP.NET Core Web API · Entity Framework Core · AutoMapper · FluentValidation · ProblemDetails (RFC 7807) cho error response chuẩn hóa.

## Cấu trúc thư mục

```
ShipMate/
├── ShipMate.API/
│   ├── Controllers/
│   ├── Middlewares/          # GlobalExceptionHandler (ProblemDetails)
│   └── Program.cs
├── ShipMate.Application/
│   ├── DTOs/
│   ├── Exceptions/           # NotFoundException, ...
│   ├── Interfaces/
│   │   ├── Repositories/
│   │   └── Services/
│   ├── Mappings/             # AutoMapper profiles
│   ├── Services/             # Implementation
│   ├── Validators/           # FluentValidation
│   └── DependencyInjection.cs
├── ShipMate.Domain/
│   ├── Common/
│   ├── Entities/
│   └── Enums/
└── ShipMate.Infrastructure/
    ├── Persistence/
    │   ├── Configurations/
    │   └── Repositories/
    └── DependencyInjection.cs
```

## Yêu cầu môi trường

- .NET 8 SDK trở lên
- Một DB provider (SQL Server / PostgreSQL / MySQL...) — hiện đang dùng `InMemory` tạm thời, xem `ShipMate.Infrastructure/DependencyInjection.cs`

## Cài đặt & chạy dự án

```bash
git clone <repo-url>
cd ShipMate
dotnet restore
dotnet build
dotnet run --project ShipMate.API
```

Sau khi chạy, mở Swagger UI tại `https://localhost:<port>/swagger` để test API.

## Team

| Vai trò | Thành viên |
|---|---|
| Mentor | Lê Nguyễn Sơn Vũ (vulns@fpt.edu.vn) |
| Leader / Backend | Hoàng Gia Bảo |
| Backend | Đỗ Thành Long |
| Frontend | Chu Nhật Lâm |
| Frontend | Đặng Hoàng Phúc |
| Frontend | Trần Đăng Khoa |

## Đóng góp

Xem quy trình làm việc, quy ước code và checklist trước khi mở PR tại [CONTRIBUTING.md](CONTRIBUTING.md).
