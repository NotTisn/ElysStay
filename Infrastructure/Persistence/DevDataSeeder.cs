using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

/// <summary>
/// Seeds a realistic alpha-testing dataset into a fresh Development database.
/// Idempotent: skips completely if any seed User already exists.
///
/// Seeded Keycloak accounts (password: Demo@123 for all):
///   demo-owner@elysstay.com   — Chủ nhà, quản lý toàn bộ tòa nhà
///   demo-staff@elysstay.com   — Nhân viên, được giao tòa nhà An Phú
///   demo-tenant1@elysstay.com — Khách thuê đang ở phòng 101 (hợp đồng active)
///   demo-tenant2@elysstay.com — Khách thuê đang ở phòng 201 (hợp đồng active)
///   demo-tenant3@elysstay.com — Khách thuê có đặt cọc phòng 102 (confirmed)
/// </summary>
public static class DevDataSeeder
{
    // ── Deterministic IDs tied to Keycloak realm export ─────────────────────
    // These MUST match the "id" fields in keycloak/elysstay-realm-export.json

    private static readonly Guid OwnerKeycloakGuid  = new("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid StaffKeycloakGuid  = new("a0000000-0000-0000-0000-000000000002");
    private static readonly Guid Tenant1KeycloakGuid = new("a0000000-0000-0000-0000-000000000003");
    private static readonly Guid Tenant2KeycloakGuid = new("a0000000-0000-0000-0000-000000000004");
    private static readonly Guid Tenant3KeycloakGuid = new("a0000000-0000-0000-0000-000000000005");

    // ── Stable DB entity IDs (deterministic across re-seedings) ─────────────

    private static readonly Guid OwnerId   = new("b0000000-0000-0000-0000-000000000001");
    private static readonly Guid StaffId   = new("b0000000-0000-0000-0000-000000000002");
    private static readonly Guid Tenant1Id = new("b0000000-0000-0000-0000-000000000003");
    private static readonly Guid Tenant2Id = new("b0000000-0000-0000-0000-000000000004");
    private static readonly Guid Tenant3Id = new("b0000000-0000-0000-0000-000000000005");

    private static readonly Guid BuildingId       = new("c0000000-0000-0000-0000-000000000001");
    private static readonly Guid Room101Id         = new("d0000000-0000-0000-0000-000000000001"); // Occupied
    private static readonly Guid Room102Id         = new("d0000000-0000-0000-0000-000000000002"); // Booked
    private static readonly Guid Room103Id         = new("d0000000-0000-0000-0000-000000000003"); // Available
    private static readonly Guid Room201Id         = new("d0000000-0000-0000-0000-000000000004"); // Occupied
    private static readonly Guid Room202Id         = new("d0000000-0000-0000-0000-000000000005"); // Available
    private static readonly Guid Room203Id         = new("d0000000-0000-0000-0000-000000000006"); // Maintenance

    private static readonly Guid ElecServiceId     = new("e0000000-0000-0000-0000-000000000001");
    private static readonly Guid WaterServiceId    = new("e0000000-0000-0000-0000-000000000002");
    private static readonly Guid InternetServiceId = new("e0000000-0000-0000-0000-000000000003");

    private static readonly Guid Contract1Id       = new("f0000000-0000-0000-0000-000000000001"); // Room 101, Tenant1
    private static readonly Guid Contract2Id       = new("f0000000-0000-0000-0000-000000000002"); // Room 201, Tenant2
    private static readonly Guid Reservation3Id    = new("f0000000-0000-0000-0000-000000000003"); // Room 102, Tenant3 (Confirmed)

    public static async Task SeedAsync(ApplicationDbContext db, ILogger logger)
    {
        // Idempotency guard — check for the seed owner
        var alreadySeeded = await db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.KeycloakId == OwnerKeycloakGuid.ToString());

        if (alreadySeeded)
        {
            logger.LogDebug("Dev seed already present - skipping");
            return;
        }

        logger.LogInformation("Seeding development database with demo data...");

        // ── 1. Users ────────────────────────────────────────────────────────

        var owner = new User
        {
            Id = OwnerId,
            KeycloakId = OwnerKeycloakGuid.ToString(),
            Email = "demo-owner@elysstay.com",
            FullName = "Nguyễn Văn An",
            Phone = "0901234567",
            Role = UserRole.Owner,
            Status = UserStatus.Active,
        };

        var staff = new User
        {
            Id = StaffId,
            KeycloakId = StaffKeycloakGuid.ToString(),
            Email = "demo-staff@elysstay.com",
            FullName = "Trần Thị Bình",
            Phone = "0912345678",
            Role = UserRole.Staff,
            Status = UserStatus.Active,
        };

        var tenant1 = new User
        {
            Id = Tenant1Id,
            KeycloakId = Tenant1KeycloakGuid.ToString(),
            Email = "demo-tenant1@elysstay.com",
            FullName = "Lê Hoàng Cường",
            Phone = "0923456789",
            Role = UserRole.Tenant,
            Status = UserStatus.Active,
        };

        var tenant2 = new User
        {
            Id = Tenant2Id,
            KeycloakId = Tenant2KeycloakGuid.ToString(),
            Email = "demo-tenant2@elysstay.com",
            FullName = "Phạm Minh Dũng",
            Phone = "0934567890",
            Role = UserRole.Tenant,
            Status = UserStatus.Active,
        };

        var tenant3 = new User
        {
            Id = Tenant3Id,
            KeycloakId = Tenant3KeycloakGuid.ToString(),
            Email = "demo-tenant3@elysstay.com",
            FullName = "Võ Thanh Hùng",
            Phone = "0945678901",
            Role = UserRole.Tenant,
            Status = UserStatus.Active,
        };

        db.Users.AddRange(owner, staff, tenant1, tenant2, tenant3);

        // ── 2. Tenant Profiles ───────────────────────────────────────────────

        db.TenantProfiles.AddRange(
            new TenantProfile
            {
                UserId = Tenant1Id,
                IdNumber = "079201012345",
                DateOfBirth = new DateOnly(1995, 3, 15),
                Gender = "Nam",
                PermanentAddress = "12 Đường Láng, Đống Đa, Hà Nội",
                IssuedDate = new DateOnly(2020, 5, 10),
                IssuedPlace = "Cục Cảnh sát QLHC về TTXH",
            },
            new TenantProfile
            {
                UserId = Tenant2Id,
                IdNumber = "079201067890",
                DateOfBirth = new DateOnly(1998, 7, 22),
                Gender = "Nam",
                PermanentAddress = "45 Nguyễn Trãi, Thanh Xuân, Hà Nội",
                IssuedDate = new DateOnly(2021, 2, 20),
                IssuedPlace = "Cục Cảnh sát QLHC về TTXH",
            },
            new TenantProfile
            {
                UserId = Tenant3Id,
            }
        );

        // ── 3. Building ──────────────────────────────────────────────────────

        var building = new Building
        {
            Id = BuildingId,
            OwnerId = OwnerId,
            Name = "Chung cư Mini An Phú",
            Address = "15 Đường 3 Tháng 2, Phường 11, Quận 10, TP.HCM",
            Description = "Tòa nhà mini cao cấp, đầy đủ tiện nghi, bảo vệ 24/7",
            TotalFloors = 2,
            InvoiceDueDay = 10,
        };

        db.Buildings.Add(building);

        // ── 4. Staff Assignment ──────────────────────────────────────────────

        db.StaffAssignments.Add(new StaffAssignment
        {
            BuildingId = BuildingId,
            StaffId = StaffId,
            AssignedAt = DateTime.UtcNow.AddMonths(-4),
        });

        // ── 5. Services ──────────────────────────────────────────────────────

        var elec = new Service
        {
            Id = ElecServiceId,
            BuildingId = BuildingId,
            Name = "Điện",
            Unit = "kWh",
            UnitPrice = 3_500,
            PreviousUnitPrice = 3_200,
            PriceUpdatedAt = DateTime.UtcNow.AddMonths(-2),
            IsMetered = true,
            IsActive = true,
        };

        var water = new Service
        {
            Id = WaterServiceId,
            BuildingId = BuildingId,
            Name = "Nước",
            Unit = "m³",
            UnitPrice = 15_000,
            PreviousUnitPrice = 15_000,
            PriceUpdatedAt = DateTime.UtcNow.AddMonths(-6),
            IsMetered = true,
            IsActive = true,
        };

        var internet = new Service
        {
            Id = InternetServiceId,
            BuildingId = BuildingId,
            Name = "Internet",
            Unit = "tháng",
            UnitPrice = 150_000,
            PreviousUnitPrice = 150_000,
            PriceUpdatedAt = DateTime.UtcNow.AddMonths(-6),
            IsMetered = false,
            IsActive = true,
        };

        db.Services.AddRange(elec, water, internet);

        // ── 6. Rooms ─────────────────────────────────────────────────────────

        var room101 = new Room
        {
            Id = Room101Id,
            BuildingId = BuildingId,
            RoomNumber = "101",
            Floor = 1,
            Area = 28,
            Price = 4_500_000,
            MaxOccupants = 2,
            Status = RoomStatus.Occupied,
        };

        var room102 = new Room
        {
            Id = Room102Id,
            BuildingId = BuildingId,
            RoomNumber = "102",
            Floor = 1,
            Area = 28,
            Price = 4_500_000,
            MaxOccupants = 2,
            Status = RoomStatus.Reserved,
        };

        var room103 = new Room
        {
            Id = Room103Id,
            BuildingId = BuildingId,
            RoomNumber = "103",
            Floor = 1,
            Area = 35,
            Price = 5_500_000,
            MaxOccupants = 3,
            Status = RoomStatus.Available,
        };

        var room201 = new Room
        {
            Id = Room201Id,
            BuildingId = BuildingId,
            RoomNumber = "201",
            Floor = 2,
            Area = 28,
            Price = 4_800_000,
            MaxOccupants = 2,
            Status = RoomStatus.Occupied,
        };

        var room202 = new Room
        {
            Id = Room202Id,
            BuildingId = BuildingId,
            RoomNumber = "202",
            Floor = 2,
            Area = 35,
            Price = 5_800_000,
            MaxOccupants = 3,
            Status = RoomStatus.Available,
        };

        var room203 = new Room
        {
            Id = Room203Id,
            BuildingId = BuildingId,
            RoomNumber = "203",
            Floor = 2,
            Area = 28,
            Price = 4_800_000,
            MaxOccupants = 2,
            Status = RoomStatus.Maintenance,
        };

        db.Rooms.AddRange(room101, room102, room103, room201, room202, room203);

        // ── 7. Reservations ──────────────────────────────────────────────────
        //   Room 102 → Tenant3: Confirmed, deposit paid, expiring in 5 days

        var reservation3 = new RoomReservation
        {
            Id = Reservation3Id,
            RoomId = Room102Id,
            TenantUserId = Tenant3Id,
            DepositAmount = 4_500_000,
            Status = ReservationStatus.Confirmed,
            ExpiresAt = DateTime.UtcNow.AddDays(5),
            Note = "Khách đã xem phòng và xác nhận, chờ ký hợp đồng",
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
        };

        db.RoomReservations.Add(reservation3);

        // ── 8. Contracts ─────────────────────────────────────────────────────

        var now = DateTime.UtcNow;
        var contractStart1 = DateOnly.FromDateTime(now.AddMonths(-4));
        var contractStart2 = DateOnly.FromDateTime(now.AddMonths(-3));

        var contract1 = new Contract
        {
            Id = Contract1Id,
            RoomId = Room101Id,
            TenantUserId = Tenant1Id,
            CreatedBy = OwnerId,
            MonthlyRent = 4_500_000,
            DepositAmount = 9_000_000,
            DepositStatus = DepositStatus.Held,
            Status = ContractStatus.Active,
            StartDate = contractStart1,
            MoveInDate = contractStart1,
            EndDate = contractStart1.AddMonths(12),
        };

        var contract2 = new Contract
        {
            Id = Contract2Id,
            RoomId = Room201Id,
            TenantUserId = Tenant2Id,
            CreatedBy = OwnerId,
            MonthlyRent = 4_800_000,
            DepositAmount = 9_600_000,
            DepositStatus = DepositStatus.Held,
            Status = ContractStatus.Active,
            StartDate = contractStart2,
            MoveInDate = contractStart2,
            EndDate = contractStart2.AddMonths(12),
        };

        db.Contracts.AddRange(contract1, contract2);

        // ── 9. Reservation deposit payment (Tenant3) ──────────────────────────

        db.Payments.Add(new Payment
        {
            Id = NewDeterministicId("pay-res3-deposit"),
            InvoiceId = null,
            ReservationId = Reservation3Id,
            RecordedBy = StaffId,
            Amount = 4_500_000,
            PaymentMethod = "Chuyển khoản ngân hàng",
            Type = PaymentType.DepositIn,
            Note = "Đặt cọc đặt phòng qua app",
            PaidAt = DateTime.UtcNow.AddDays(-2),
        });

        // ── 10. Invoices + Meter Readings + Payments (3 past months) ─────────

        await db.SaveChangesAsync(); // flush FKs before adding invoices

        for (int offset = 3; offset >= 1; offset--)
        {
            var billDate = now.AddMonths(-offset);
            var billingMonth = billDate.Month;
            var billingYear = billDate.Year;
            var dueDate = new DateOnly(billingYear, billingMonth, 10);
            if (dueDate.Day < 10) dueDate = dueDate.AddMonths(1);

            // ── Meter readings for occupied rooms ─────────────────────────────

            int prevElec1 = 100 + (3 - offset) * 80;
            int currElec1 = prevElec1 + 80;
            int prevWater1 = 20 + (3 - offset) * 8;
            int currWater1 = prevWater1 + 8;

            db.MeterReadings.AddRange(
                new MeterReading
                {
                    Id = NewDeterministicId($"mr-elec-101-{billingYear}-{billingMonth}"),
                    RoomId = Room101Id,
                    ServiceId = ElecServiceId,
                    CreatedBy = StaffId,
                    BillingMonth = billingMonth,
                    BillingYear = billingYear,
                    PreviousReading = prevElec1,
                    CurrentReading = currElec1,
                    Consumption = currElec1 - prevElec1,
                    DateRead = billDate,
                },
                new MeterReading
                {
                    Id = NewDeterministicId($"mr-water-101-{billingYear}-{billingMonth}"),
                    RoomId = Room101Id,
                    ServiceId = WaterServiceId,
                    CreatedBy = StaffId,
                    BillingMonth = billingMonth,
                    BillingYear = billingYear,
                    PreviousReading = prevWater1,
                    CurrentReading = currWater1,
                    Consumption = currWater1 - prevWater1,
                    DateRead = billDate,
                },
                new MeterReading
                {
                    Id = NewDeterministicId($"mr-elec-201-{billingYear}-{billingMonth}"),
                    RoomId = Room201Id,
                    ServiceId = ElecServiceId,
                    CreatedBy = StaffId,
                    BillingMonth = billingMonth,
                    BillingYear = billingYear,
                    PreviousReading = 50 + (3 - offset) * 65,
                    CurrentReading = 50 + (3 - offset) * 65 + 65,
                    Consumption = 65,
                    DateRead = billDate,
                },
                new MeterReading
                {
                    Id = NewDeterministicId($"mr-water-201-{billingYear}-{billingMonth}"),
                    RoomId = Room201Id,
                    ServiceId = WaterServiceId,
                    CreatedBy = StaffId,
                    BillingMonth = billingMonth,
                    BillingYear = billingYear,
                    PreviousReading = 10 + (3 - offset) * 6,
                    CurrentReading = 10 + (3 - offset) * 6 + 6,
                    Consumption = 6,
                    DateRead = billDate,
                }
            );

            // ── Invoices ────────────────────────────────────────────────────

            // Contract1 (Room 101)
            decimal svc1 = (80 * 3_500m) + (8 * 15_000m) + 150_000m; // elec + water + internet
            var inv1Id = NewDeterministicId($"inv-c1-{billingYear}-{billingMonth}");
            var invoice1 = new Invoice
            {
                Id = inv1Id,
                ContractId = Contract1Id,
                CreatedBy = OwnerId,
                BillingMonth = billingMonth,
                BillingYear = billingYear,
                RentAmount = 4_500_000,
                ServiceAmount = svc1,
                PenaltyAmount = 0,
                DiscountAmount = 0,
                TotalAmount = 4_500_000 + svc1,
                Status = InvoiceStatus.Paid,
                DueDate = dueDate,
                UpdatedAt = billDate.AddDays(5),
            };

            // Contract2 (Room 201)
            decimal svc2 = (65 * 3_500m) + (6 * 15_000m) + 150_000m;
            var inv2Id = NewDeterministicId($"inv-c2-{billingYear}-{billingMonth}");
            var invoice2 = new Invoice
            {
                Id = inv2Id,
                ContractId = Contract2Id,
                CreatedBy = OwnerId,
                BillingMonth = billingMonth,
                BillingYear = billingYear,
                RentAmount = 4_800_000,
                ServiceAmount = svc2,
                PenaltyAmount = 0,
                DiscountAmount = 0,
                TotalAmount = 4_800_000 + svc2,
                Status = InvoiceStatus.Paid,
                DueDate = dueDate,
                UpdatedAt = billDate.AddDays(6),
            };

            db.Invoices.AddRange(invoice1, invoice2);
            await db.SaveChangesAsync();

            // Payments for paid invoices
            db.Payments.AddRange(
                new Payment
                {
                    Id = NewDeterministicId($"pay-inv1-{billingYear}-{billingMonth}"),
                    InvoiceId = inv1Id,
                    RecordedBy = StaffId,
                    Amount = invoice1.TotalAmount,
                    PaymentMethod = "Chuyển khoản ngân hàng",
                    Type = PaymentType.RentPayment,
                    PaidAt = billDate.AddDays(5),
                },
                new Payment
                {
                    Id = NewDeterministicId($"pay-inv2-{billingYear}-{billingMonth}"),
                    InvoiceId = inv2Id,
                    RecordedBy = StaffId,
                    Amount = invoice2.TotalAmount,
                    PaymentMethod = "Tiền mặt",
                    Type = PaymentType.RentPayment,
                    PaidAt = billDate.AddDays(6),
                }
            );
        }

        // ── 11. Current month invoices (Sent — unpaid, ready for demo) ────────

        var curMonth = now.Month;
        var curYear = now.Year;
        var dueYear = curMonth == 12 ? curYear + 1 : curYear;
        var dueMonth = curMonth == 12 ? 1 : curMonth + 1;
        var curDue = new DateOnly(dueYear, dueMonth, 10);

        decimal curSvc1 = (80 * 3_500m) + (8 * 15_000m) + 150_000m;
        var curInv1Id = NewDeterministicId($"inv-c1-{curYear}-{curMonth}");
        db.Invoices.Add(new Invoice
        {
            Id = curInv1Id,
            ContractId = Contract1Id,
            CreatedBy = OwnerId,
            BillingMonth = curMonth,
            BillingYear = curYear,
            RentAmount = 4_500_000,
            ServiceAmount = curSvc1,
            PenaltyAmount = 0,
            DiscountAmount = 0,
            TotalAmount = 4_500_000 + curSvc1,
            Status = InvoiceStatus.Sent,
            DueDate = curDue,
        });

        decimal curSvc2 = (65 * 3_500m) + (6 * 15_000m) + 150_000m;
        var curInv2Id = NewDeterministicId($"inv-c2-{curYear}-{curMonth}");
        db.Invoices.Add(new Invoice
        {
            Id = curInv2Id,
            ContractId = Contract2Id,
            CreatedBy = OwnerId,
            BillingMonth = curMonth,
            BillingYear = curYear,
            RentAmount = 4_800_000,
            ServiceAmount = curSvc2,
            PenaltyAmount = 0,
            DiscountAmount = 0,
            TotalAmount = 4_800_000 + curSvc2,
            Status = InvoiceStatus.PartiallyPaid,
            DueDate = curDue,
        });

        await db.SaveChangesAsync();

        // Partial payment on curInv2
        db.Payments.Add(new Payment
        {
            Id = NewDeterministicId($"pay-partial-c2-{curYear}-{curMonth}"),
            InvoiceId = curInv2Id,
            RecordedBy = StaffId,
            Amount = 4_800_000, // paid rent portion only
            PaymentMethod = "Chuyển khoản ngân hàng",
            Type = PaymentType.RentPayment,
            Note = "Thanh toán tiền thuê, chưa thanh toán tiền dịch vụ",
            PaidAt = now.AddDays(-1),
        });

        // ── 12. Expenses ─────────────────────────────────────────────────────

        db.Expenses.AddRange(
            new Expense
            {
                Id = NewDeterministicId("expense-repair-203"),
                BuildingId = BuildingId,
                RoomId = Room203Id,
                Category = "Repair",
                Description = "Sửa chữa hệ thống điện phòng 203 — thay công tắc và ổ cắm bị hỏng",
                Amount = 850_000,
                ExpenseDate = DateOnly.FromDateTime(now.AddDays(-10)),
                RecordedBy = StaffId,
            },
            new Expense
            {
                Id = NewDeterministicId("expense-cleaning"),
                BuildingId = BuildingId,
                Category = "Cleaning",
                Description = "Vệ sinh hành lang và khu vực chung tháng " + curMonth,
                Amount = 500_000,
                ExpenseDate = DateOnly.FromDateTime(now.AddDays(-5)),
                RecordedBy = StaffId,
            },
            new Expense
            {
                Id = NewDeterministicId("expense-elevator"),
                BuildingId = BuildingId,
                Category = "Maintenance",
                Description = "Bảo trì định kỳ hệ thống thang máy quý I/2026",
                Amount = 2_200_000,
                ExpenseDate = DateOnly.FromDateTime(now.AddMonths(-1)),
                RecordedBy = OwnerId,
            }
        );

        // ── 13. Maintenance issue ─────────────────────────────────────────────

        db.MaintenanceIssues.AddRange(
            new MaintenanceIssue
            {
                Id = NewDeterministicId("issue-elec-203"),
                BuildingId = BuildingId,
                RoomId = Room203Id,
                ReportedBy = StaffId,
                AssignedTo = StaffId,
                Title = "Hệ thống điện phòng 203 gặp sự cố",
                Description = "Cầu dao tự động ngắt liên tục, không xác định nguyên nhân. Phòng tạm thời không thể cho thuê trong khi chờ sửa chữa hoàn tất.",
                Status = IssueStatus.InProgress,
                Priority = PriorityLevel.High,
                CreatedAt = now.AddDays(-12),
            },
            new MaintenanceIssue
            {
                Id = NewDeterministicId("issue-waterleak-102"),
                BuildingId = BuildingId,
                RoomId = Room102Id,
                ReportedBy = Tenant3Id,
                AssignedTo = StaffId,
                Title = "Vòi nước bồn rửa bị rỉ nhẹ",
                Description = "Khách phản ánh vòi nước bồn rửa bát bị rỉ khi tắt hoàn toàn. Cần thay ron.",
                Status = IssueStatus.New,
                Priority = PriorityLevel.Low,
                CreatedAt = now.AddDays(-1),
            }
        );

        // ── 14. Notifications for Owner & Staff ───────────────────────────────

        db.Notifications.AddRange(
            new Notification
            {
                UserId = OwnerId,
                Title = "Hóa đơn tháng " + curMonth + " đã gửi",
                Message = "2 hóa đơn tháng " + curMonth + "/" + curYear + " đã được gửi đến khách thuê.",
                Type = "INVOICE_SENT",
                ReferenceId = curInv1Id,
                IsRead = false,
                CreatedAt = now.AddDays(-3),
            },
            new Notification
            {
                UserId = OwnerId,
                Title = "Thanh toán một phần — Phạm Minh Dũng",
                Message = "Phạm Minh Dũng đã thanh toán tiền thuê phòng 201. Còn lại tiền dịch vụ chưa thanh toán.",
                Type = "PAYMENT_RECORDED",
                ReferenceId = curInv2Id,
                IsRead = false,
                CreatedAt = now.AddDays(-1),
            },
            new Notification
            {
                UserId = StaffId,
                Title = "Sự cố mới được báo cáo",
                Message = "Võ Thanh Hùng báo cáo vòi nước phòng 102 bị rỉ. Vui lòng kiểm tra.",
                Type = "ISSUE",
                ReferenceId = NewDeterministicId("issue-waterleak-102"),
                IsRead = false,
                CreatedAt = now.AddDays(-1),
            }
        );

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Dev seed complete - {UserCount} users, 1 building, {RoomCount} rooms, 2 contracts, 1 reservation, {InvoiceCount} invoices",
            5, 6, 8);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a deterministic Guid from a string key using MD5.
    /// Same key always produces the same Guid, so re-seeding is idempotent.
    /// </summary>
    private static Guid NewDeterministicId(string key)
    {
        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes("elysstay-seed:" + key));
        return new Guid(hash);
    }
}
