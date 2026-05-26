# ElysStay Backend — Project Context

## Tổng quan

ElysStay là hệ thống quản lý cho thuê nhà trọ tại Việt Nam, thay thế quy trình thủ công (sổ sách, chat, Excel) bằng một nền tảng số hoá toàn bộ vòng đời: toà nhà → phòng → hợp đồng → chỉ số điện nước → hoá đơn → thanh toán → bảo trì.

**Stack chính:** .NET 10 · ASP.NET Core · EF Core + PostgreSQL · MediatR · Keycloak · Testcontainers

---

## Kiến trúc

Dự án theo **Clean Architecture** kết hợp **CQRS** (Command Query Responsibility Segregation):

```
HTTP Request
    │
    ▼
[API Layer]                 Controllers, Middleware, JWT auth
    │  _mediator.Send(Command/Query)
    ▼
[MediatR Pipeline]          ValidationBehavior → LoggingBehavior
    │
    ▼
[Application Layer]         Handlers, DTOs, Validators (FluentValidation)
    │  IApplicationDbContext
    ▼
[Infrastructure Layer]      EF Core DbContext, Keycloak, Email, PDF, OCR
    │
    ▼
[PostgreSQL]
```

**Quy tắc phụ thuộc:** `Domain ← Application ← Infrastructure ← API`. Không layer nào phụ thuộc ngược lại.

### Layers

| Layer | Project | Mục đích |
|---|---|---|
| `API` | `API.csproj` | Controllers, Middleware, Program.cs, Swagger |
| `Application` | `Application.csproj` | Handlers, DTOs, Validators, Interfaces |
| `Domain` | `Domain.csproj` | Entities, Enums, Constants (không phụ thuộc gì) |
| `Infrastructure` | `Infrastructure.csproj` | EF Core, Keycloak, Email, PDF, OCR, Background Jobs |
| `Tests.Unit` | `Tests.Unit.csproj` | Unit test handlers với Moq + MockQueryable |
| `Tests.Integration` | `Tests.Integration.csproj` | Integration test với PostgreSQL Testcontainers |
| `Tests.Acceptance` | `Tests.Acceptance.csproj` | BDD acceptance test với SpecFlow |

---

## Domain Model

### Entities (18 entities)

```
User
 ├── Building (OwnerId → User)
 │    ├── Room (BuildingId → Building)
 │    │    ├── RoomService (override giá/số lượng)
 │    │    ├── MeterReading (chỉ số điện nước hàng tháng)
 │    │    └── Contract (hợp đồng thuê)
 │    │         ├── ContractTenant (danh sách người ở)
 │    │         ├── Invoice (hoá đơn hàng tháng)
 │    │         │    ├── InvoiceDetail (dòng chi tiết)
 │    │         │    └── Payment (thanh toán)
 │    │         └── Payment (cọc, hoàn cọc)
 │    ├── Service (dịch vụ của toà nhà)
 │    ├── StaffAssignment (nhân viên → toà nhà)
 │    ├── Expense (chi phí vận hành)
 │    └── MaintenanceIssue (sự cố bảo trì)
 ├── RoomReservation (đặt cọc giữ phòng)
 ├── TenantProfile (hồ sơ khách thuê)
 └── Notification
```

### Enums chính

| Enum | Values |
|---|---|
| `UserRole` | Owner, Staff, Tenant |
| `RoomStatus` | Available, Booked, Occupied, Maintenance |
| `ContractStatus` | Active, Terminated |
| `InvoiceStatus` | Draft, Sent, PartiallyPaid, Unpaid, Paid, Overdue, Void |
| `ReservationStatus` | Pending, Confirmed, Converted, Cancelled, Expired |
| `PaymentType` | RentPayment, DepositIn, DepositRefund |
| `DepositStatus` | Held, Refunded, Forfeited |

---

## Application Layer — Features

Mỗi feature nằm trong `Application/Features/{Feature}/` theo cấu trúc:

```
Features/
├── Buildings/
│   ├── Commands/       CreateBuilding, UpdateBuilding, DeleteBuilding
│   ├── Queries/        GetBuildingById, GetBuildings
│   ├── DTOs/
│   └── Validators/
├── Contracts/
├── Dashboard/
├── Expenses/
├── Invoices/           ← core feature (generate, update, void, send, PDF)
├── MaintenanceIssues/
├── MeterReadings/
├── Notifications/
├── Payments/
├── Reservations/
├── Rooms/
├── RoomServices/
├── Services/
├── StaffAssignments/
├── TenantProfiles/
└── Users/
```

### Luồng request điển hình

```csharp
// 1. Controller
[HttpPost("generate")]
public Task<IActionResult> Generate(GenerateInvoicesCommand cmd, CancellationToken ct)
    => HandleAsync(() => _mediator.Send(cmd, ct));

// 2. MediatR → ValidationBehavior (FluentValidation) → LoggingBehavior

// 3. Handler
public class GenerateInvoicesCommandHandler : IRequestHandler<GenerateInvoicesCommand, InvoiceGenerationResult>
{
    // Inject: IApplicationDbContext, ICurrentUserService, IBuildingScopeService
    // Logic: authorize → query DB → business rules → save → return DTO
}
```

---

## Business Rules Quan Trọng

### Authorization
| Mã | Quy tắc |
|---|---|
| AUTH-05 | Staff chỉ truy cập toà nhà được phân công (StaffAssignment) |
| AUTH-06 | Tenant tự lọc theo JWT UserId — không cần route riêng |

### State Machine — Phòng
```
AVAILABLE → BOOKED (tạo đặt cọc)
BOOKED → OCCUPIED (tạo hợp đồng)
OCCUPIED → AVAILABLE (thanh lý hợp đồng)
BOOKED → AVAILABLE (hủy/hết hạn đặt cọc)
AVAILABLE ↔ MAINTENANCE (thủ công)
```

### State Machine — Hoá đơn (SM-11, SM-12)
```
DRAFT → SENT → PARTIALLY_PAID ──→ PAID
                              ↘→ VOID (chỉ Owner, trừ PAID)
              OVERDUE ─────────→ PAID / VOID
```

### Tạo hoá đơn (Invoice Generation)
| Mã | Quy tắc |
|---|---|
| IG-01 | **Idempotent** — bỏ qua nếu hoá đơn đã tồn tại cho contract+tháng đó (trừ Void) |
| IG-02 | Thiếu chỉ số đồng hồ → bỏ qua dòng dịch vụ đó + warning, **không** chặn toàn hoá đơn |
| IG-03 | Dịch vụ đồng hồ: `consumption × (OverrideUnitPrice ?? UnitPrice)` |
| IG-04 | Dịch vụ cố định: `quantity × price`, quantity = `OverrideQuantity ?? occupantCount ?? 1` |
| IG-06 | DueDate = tháng sau / `Building.InvoiceDueDay` |
| IG-07 | Status bắt đầu là **DRAFT** |
| IG-08 | Toàn bộ batch trong **1 DB transaction** |

### Tính tiền phòng theo tỷ lệ (Proration)
| Mã | Quy tắc |
|---|---|
| PR-05 | Tháng đầu: `Round(MonthlyRent / daysInMonth × daysFromMoveIn)` |
| PR-06 | Tháng cuối: tương tự theo `TerminationDate` |
| PR-01 | `Contract.MonthlyRent` khoá khi ký — đổi giá phòng không ảnh hưởng HĐ cũ |
| PR-04 | Giá ưu tiên: `OverrideUnitPrice ?? Service.UnitPrice` |

### Unique Constraints
| Mã | Ràng buộc |
|---|---|
| UQ-01 | Chỉ 1 hợp đồng ACTIVE mỗi phòng |
| UQ-02 | Chỉ 1 hoá đơn mỗi contract mỗi tháng |
| UQ-03 | Chỉ 1 chỉ số mỗi phòng/dịch vụ/tháng |
| UQ-04 | Số phòng duy nhất trong toà nhà |

### Soft Delete
- `User`, `Building`, `Room` có trường `DeletedAt` — list queries tự loại trừ
- Không xoá toà nhà/phòng nếu còn hợp đồng ACTIVE (409 Conflict)
- Dịch vụ dùng `IsActive = false` thay vì xoá

---

## Infrastructure

### Authentication — Keycloak
- JWT Bearer Token, TTL 30 phút / Refresh 7 ngày
- `UserAutoProvisioningMiddleware`: tự tạo User trong DB lần đầu đăng nhập
- `CurrentUserService`: đọc claims từ JWT, expose `UserId`, `Role`, `IsOwner`, `IsStaff`
- `BuildingScopeService`: kiểm tra quyền truy cập toà nhà (Owner → OwnerId, Staff → StaffAssignment)

### Persistence — EF Core + PostgreSQL
- `ApplicationDbContext` implement `IApplicationDbContext` (interface cho Application layer)
- `UpdatedAt` tự cập nhật qua `SaveChangesAsync` override
- 17 entity configurations trong `Infrastructure/Persistence/Configurations/`
- 7 migrations (latest: `FixInvoiceUniqueConstraintForVoids`)

### External Services
| Service | Provider | Dùng cho |
|---|---|---|
| Email | Resend API | Thông báo hoá đơn, hợp đồng |
| File Upload | Cloudinary | Ảnh phòng, tài liệu |
| PDF | QuestPDF | Xuất hoá đơn PDF |
| OCR | FPT AI | Đọc chỉ số đồng hồ từ ảnh |

### Background Jobs
| Job | Tần suất | Chức năng |
|---|---|---|
| `InvoiceOverdueBackgroundService` | Hàng ngày | Chuyển hoá đơn quá hạn → Overdue |
| `ContractExpiryAlertBackgroundService` | Hàng ngày | Cảnh báo hợp đồng sắp hết hạn |
| `ReservationExpiryBackgroundService` | Mỗi giờ | Chuyển đặt cọc hết hạn → Expired |

---

## API Controllers (17 controllers)

| Controller | Prefix | Chức năng chính |
|---|---|---|
| `BuildingsController` | `/api/buildings` | CRUD toà nhà |
| `RoomsController` | `/api/rooms` | CRUD phòng, đổi trạng thái |
| `ContractsController` | `/api/contracts` | CRUD hợp đồng, thanh lý |
| `InvoicesController` | `/api/invoices` | Generate, update, void, send, PDF |
| `MeterReadingsController` | `/api/meter-readings` | Nhập chỉ số điện nước |
| `PaymentsController` | `/api/payments` | Ghi nhận thanh toán |
| `ServicesController` | `/api/services` | CRUD dịch vụ |
| `RoomServicesController` | `/api/room-services` | Override giá/số lượng dịch vụ theo phòng |
| `ReservationsController` | `/api/reservations` | Đặt cọc, chuyển đổi HĐ |
| `StaffAssignmentsController` | `/api/staff-assignments` | Phân công nhân viên |
| `ExpensesController` | `/api/expenses` | Chi phí vận hành |
| `IssuesController` | `/api/issues` | Sự cố bảo trì |
| `UsersController` | `/api/users` | Quản lý người dùng |
| `TenantProfilesController` | `/api/tenant-profiles` | Hồ sơ khách thuê |
| `NotificationsController` | `/api/notifications` | Thông báo |
| `ReportsController` | `/api/reports` | Dashboard, báo cáo tài chính |
| `BaseApiController` | — | Base class: `HandleAsync`, `ApiResponse<T>` |

---

## Testing Strategy

```
Tests.Acceptance/   BDD (SpecFlow + Gherkin)    ← kiểm tra business scenarios
Tests.Integration/  PostgreSQL Testcontainers   ← kiểm tra handler + DB thật
Tests.Unit/         Moq + MockQueryable         ← kiểm tra handler logic đơn lẻ
```

### Unit Tests — Pattern
```csharp
// Mock dependencies, call real handler, assert results/exceptions
var _db           = new Mock<IApplicationDbContext>();
var _currentUser  = new Mock<ICurrentUserService>();
var _buildingScope = new Mock<IBuildingScopeService>();

// MockQueryable.Moq để mock DbSet<T> async
var mockSet = entities.AsQueryable().BuildMockDbSet();
_db.Setup(m => m.Invoices).Returns(mockSet.Object);
```

### Integration Tests — Pattern
```csharp
// Real PostgreSQL (Testcontainers), real handler, mocked auth only
public class XxxIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new(); // spins up postgres:15

    // Create handler with real DbContext + mocked ICurrentUserService
    private XxxCommandHandler CreateHandler() => new(
        _fixture.DbContext,
        currentUserMock.Object,
        new BuildingScopeService(_fixture.DbContext, currentUserMock.Object));
}
```

### Test Data Builder
`TestDataBuilder` trong `Tests.Integration/Builders/` — factory methods cho mọi entity:
`CreateUser`, `CreateBuilding`, `CreateRoom`, `CreateContract`, `CreateService`, `CreateMeterReading`, `CreateInvoice`, `CreatePayment`

---

## Quy ước Code

### Naming
- Command/Query: `{Action}{Entity}Command` / `{Action}{Entity}Query`
- Handler: `{Command/Query}Handler` (cùng file với command/query)
- DTO: `{Entity}Dto`, `{Entity}DetailDto`

### Error Handling
```csharp
throw new NotFoundException("Hoá đơn", id);      // 404
throw new ConflictException("Mô tả lý do");       // 409
throw new BadRequestException("Mô tả lỗi");       // 400
throw new ForbiddenException("Không có quyền");   // 403
```
`GlobalExceptionHandlingMiddleware` chuyển exception → HTTP response chuẩn.

### Response Pattern
```csharp
// Controller base method
return await HandleAsync(() => _mediator.Send(command, ct));
// → ApiResponse<T> { Success, Data, Message }
```

### Validation
FluentValidation validators tự chạy qua `ValidationBehavior` trước khi handler được gọi.

---

## Môi trường Development

```bash
# Khởi động toàn bộ infrastructure (Postgres + Keycloak)
dev.bat

# Hoặc thủ công
docker-compose up -d
dotnet run --project API
```

**Ports mặc định:**
- API: `https://localhost:7xxx`
- PostgreSQL: `5432`
- Keycloak: `8080`

**Environment variables:** Xem `.env.example`

---

## File quan trọng cần biết

| File | Mục đích |
|---|---|
| `Application/Common/Interfaces/IApplicationDbContext.cs` | Contract DB cho Application layer |
| `Application/Common/Interfaces/ICurrentUserService.cs` | Current user identity |
| `Application/Common/Interfaces/IBuildingScopeService.cs` | Building-level authorization |
| `Infrastructure/Persistence/ApplicationDbContext.cs` | EF Core implementation |
| `Infrastructure/Auth/BuildingScopeService.cs` | Authorization logic |
| `Infrastructure/Auth/CurrentUserService.cs` | JWT claims extraction |
| `API/Program.cs` | Service registration, middleware pipeline |
| `Application/Features/Invoices/Commands/GenerateInvoicesCommand.cs` | Core invoice generation logic |
| `Tests.Integration/Fixtures/DatabaseFixture.cs` | PostgreSQL Testcontainer setup |
| `Tests.Integration/Builders/TestDataBuilder.cs` | Test entity factories |
| `docs/business-rules.csv` | Toàn bộ business rules (SM-, IG-, PR-, PAY-, ...) |
