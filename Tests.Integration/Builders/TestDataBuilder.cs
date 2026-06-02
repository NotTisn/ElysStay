using Domain.Entities;
using Domain.Enums;

namespace Tests.Integration.Builders;

/// <summary>
/// Builder pattern for test entities — simplify test data creation.
/// </summary>
public class TestDataBuilder
{
    // create tenant
    public static User CreateUser(
        string? email = null,
        string? phone = null,
        string fullName = "Test User",
        UserRole role = UserRole.Tenant,
        UserStatus status = UserStatus.Active)
    {
        email ??= $"test_{Guid.NewGuid()}@example.com";
        phone ??= $"0{Math.Abs(Guid.NewGuid().GetHashCode())}".PadRight(10, '0')[..10];
        return new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            FullName = fullName,
            Phone = phone,
            Role = role,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static Building CreateBuilding(Guid ownerId, string name = "Test Building")
    {
        return new Building
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = name,
            Address = "123 Main St, City",
            Description = "Test building",
            TotalFloors = 5,
            InvoiceDueDay = 10,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static Room CreateRoom(
        Guid buildingId,
        string roomNumber = "101",
        decimal price = 5_000_000)
    {
        return new Room
        {
            Id = Guid.NewGuid(),
            BuildingId = buildingId,
            RoomNumber = roomNumber,
            Floor = 1,
            Area = 30,
            Price = price,
            MaxOccupants = 2,
            Status = RoomStatus.Available,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static RoomReservation CreateReservation(
        Guid roomId,
        Guid tenantUserId,
        decimal depositAmount = 10_000_000,
        ReservationStatus status = ReservationStatus.Pending)
    {
        return new RoomReservation
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            TenantUserId = tenantUserId,
            DepositAmount = depositAmount,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static Contract CreateContract(
        Guid roomId,
        Guid tenantUserId,
        Guid createdBy,
        decimal monthlyRent = 5_000_000,
        decimal depositAmount = 10_000_000,
        ContractStatus status = ContractStatus.Active,
        DateOnly? MoveInDate = null,
        DateOnly? StartDate = null,
        DateOnly? EndDate = null
        )
    {
        StartDate ??= new DateOnly(2026, 1, 31);
        EndDate ??= new DateOnly(2026, 6, 30);
        return new Contract
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            TenantUserId = tenantUserId,
            CreatedBy = createdBy,
            MonthlyRent = monthlyRent,
            DepositAmount = depositAmount,
            DepositStatus = DepositStatus.Held,
            Status = status,
            StartDate = StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            MoveInDate = MoveInDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = EndDate ?? StartDate?.AddMonths(6) ?? DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(6),
            ContractTenants = new List<ContractTenant>
            {
                new() { TenantUserId = tenantUserId, IsMainTenant = true, MoveInDate = MoveInDate ?? DateOnly.FromDateTime(DateTime.UtcNow) }
            },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static Service CreateService(
        Guid buildingId,
        string name = "Water",
        string unit = "m³",
        decimal unitPrice = 10_000,
        bool isMetered = true)
    {
        return new Service
        {
            Id = Guid.NewGuid(),
            BuildingId = buildingId,
            Name = name,
            Unit = unit,
            UnitPrice = unitPrice,
            PreviousUnitPrice = unitPrice,
            PriceUpdatedAt = DateTime.UtcNow,
            IsMetered = isMetered,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static Invoice CreateInvoice(
        Guid contractId,
        Guid createdBy,
        int billingMonth = 3,
        int billingYear = 2026,
        decimal rentAmount = 5_000_000,
        InvoiceStatus status = InvoiceStatus.Draft)
    {
        return new Invoice
        {
            Id = Guid.NewGuid(),
            ContractId = contractId,
            CreatedBy = createdBy,
            BillingMonth = billingMonth,
            BillingYear = billingYear,
            RentAmount = rentAmount,
            ServiceAmount = 0,
            PenaltyAmount = 0,
            DiscountAmount = 0,
            TotalAmount = rentAmount,
            Status = status,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static MeterReading CreateMeterReading(
        Guid roomId,
        Guid serviceId,
        Guid createdBy,
        int billingMonth = 3,
        int billingYear = 2026,
        decimal previousReading = 100,
        decimal currentReading = 110)
    {
        return new MeterReading
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            ServiceId = serviceId,
            CreatedBy = createdBy,
            BillingMonth = billingMonth,
            BillingYear = billingYear,
            PreviousReading = previousReading,
            CurrentReading = currentReading,
            Consumption = currentReading - previousReading,
            DateRead = DateTime.UtcNow
        };
    }

    public static Payment CreatePayment(
        Guid? invoiceId,
        Guid recordedBy,
        decimal amount = 5_000_000,
        string method = "BankTransfer",
        PaymentType type = PaymentType.RentPayment)
    {
        return new Payment
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId,
            RecordedBy = recordedBy,
            Amount = amount,
            PaymentMethod = method,
            Type = type,
            PaidAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }
}
