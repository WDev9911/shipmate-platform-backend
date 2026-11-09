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
- PostgreSQL (mặc định `localhost:5432`, database `shipmate`)
- Công cụ `dotnet-ef`: `dotnet tool install --global dotnet-ef`

## Cài đặt & chạy dự án

```bash
git clone <repo-url>
cd ShipMate
dotnet restore

# Cấu hình: sao chép file mẫu rồi điền các giá trị (file thật bị gitignore, không commit)
cp ShipMate.API/appsettings.Development.example.json ShipMate.API/appsettings.Development.json

# Tạo bảng trong database
dotnet ef database update --project ShipMate.Infrastructure --startup-project ShipMate.API

# Tin chứng chỉ HTTPS của .NET (một lần mỗi máy), rồi chạy bằng profile https
dotnet dev-certs https --trust
dotnet run --project ShipMate.API --launch-profile https
```

Swagger UI: `https://localhost:7138/swagger`. Phải chạy profile `https`: đăng nhập và kết nối GitHub cần HTTPS.

Hướng dẫn chi tiết cho Frontend (các khóa cần điền, tạo GitHub OAuth App, lỗi thường gặp) nằm ở [docs/frontend-guide.md](docs/frontend-guide.md).

Chạy test: `dotnet test`.

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
