using Application.Common.Interfaces;
using Application.Features.Invoices.Commands;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using MockQueryable.Moq;
using Moq;
using Xunit;

namespace ElysStay.Tests.Unit.Business;

/// <summary>
/// Unit tests for GenerateInvoicesCommandHandler.
/// Maps to @Invoice and @Service scenarios in InvoiceCalculation.feature.
/// All DbSet dependencies are mocked via MockQueryable.Moq.
/// Navigation properties are pre-populated because Include() is a no-op in LINQ-to-Objects.
/// </summary>
public class InvoiceGenerationUnitTests
{
    private readonly Mock<IApplicationDbContext> _db           = new();
    private readonly Mock<ICurrentUserService>   _currentUser  = new();
    private readonly Mock<IBuildingScopeService> _buildingScope = new();

    private GenerateInvoicesCommandHandler CreateHandler()
        => new(_db.Object, _currentUser.Object, _buildingScope.Object);

    // ── Test data factory ─────────────────────────────────────────────────────

    private record Fixture(Building Building, Room Room, User Tenant, Contract Contract);

    private static Fixture CreateFixture(decimal monthlyRent = 5_000_000)
    {
        var buildingId = Guid.NewGuid();
        var building   = new Building { Id = buildingId, Name = "Tòa A", InvoiceDueDay = 10 };
        var room       = new Room     { Id = Guid.NewGuid(), BuildingId = buildingId, Building = building, RoomNumber = "101" };
        var tenant     = new User     { Id = Guid.NewGuid(), FullName = "Test Tenant", Email = "t@t.com" };
        var contract   = new Contract
        {
            Id            = Guid.NewGuid(),
            RoomId        = room.Id,
            Room          = room,
            TenantUserId  = tenant.Id,
            TenantUser    = tenant,
            MonthlyRent   = monthlyRent,
            StartDate     = new DateOnly(2026, 1, 1),
            EndDate       = new DateOnly(2027, 12, 31),
            MoveInDate    = new DateOnly(2026, 1, 1), // before billing period → no proration
            Status        = ContractStatus.Active
        };
        return new Fixture(building, room, tenant, contract);
    }

    private void SetupMocks(
        Fixture f,
        List<Invoice>?      existingInvoices = null,
        List<Service>?      services         = null,
        List<RoomService>?  roomServices     = null,
        List<MeterReading>? meterReadings    = null)
    {
        _currentUser.Setup(m => m.GetRequiredUserId()).Returns(Guid.NewGuid());
        _buildingScope
            .Setup(m => m.AuthorizeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _db.Setup(m => m.Buildings)
            .Returns(new List<Building> { f.Building }.AsQueryable().BuildMockDbSet().Object);
        _db.Setup(m => m.Contracts)
            .Returns(new List<Contract> { f.Contract }.AsQueryable().BuildMockDbSet().Object);
        _db.Setup(m => m.Invoices)
            .Returns((existingInvoices ?? []).AsQueryable().BuildMockDbSet().Object);
        _db.Setup(m => m.Services)
            .Returns((services ?? []).AsQueryable().BuildMockDbSet().Object);
        _db.Setup(m => m.RoomServices)
            .Returns((roomServices ?? []).AsQueryable().BuildMockDbSet().Object);
        _db.Setup(m => m.MeterReadings)
            .Returns((meterReadings ?? []).AsQueryable().BuildMockDbSet().Object);
        _db.Setup(m => m.InvoiceDetails)
            .Returns(new List<InvoiceDetail>().AsQueryable().BuildMockDbSet().Object);
        _db.Setup(m => m.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    // ── @Invoice: Generate invoice successfully (IG-07) ───────────────────────

    [Fact]
    public async Task Handle_NoExistingInvoice_GeneratesDraftInvoice()
    {
        // @Invoice: Generate invoice successfully → status Draft (IG-07: Status starts as DRAFT)
        var f = CreateFixture();
        SetupMocks(f);

        var result = await CreateHandler().Handle(
            new GenerateInvoicesCommand { BuildingId = f.Building.Id, BillingYear = 2026, BillingMonth = 3 }, default);

        result.Generated.Should().HaveCount(1);
        result.Generated[0].Status.Should().Be("Draft");
        result.Generated[0].RentAmount.Should().Be(5_000_000);
        result.Skipped.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NoServices_TotalEqualsRentOnly()
    {
        // @Invoice: No services → TotalAmount = RentAmount
        var f = CreateFixture();
        SetupMocks(f, services: []);

        var result = await CreateHandler().Handle(
            new GenerateInvoicesCommand { BuildingId = f.Building.Id, BillingYear = 2026, BillingMonth = 3 }, default);

        result.Generated[0].TotalAmount.Should().Be(5_000_000);
        result.Generated[0].ServiceAmount.Should().Be(0);
    }

    // ── @Invoice: Idempotency — skip if invoice already exists (IG-01) ────────

    [Fact]
    public async Task Handle_InvoiceAlreadyExists_SkipsContractAndReturnsSkippedEntry()
    {
        // @Invoice: Skip invoice generation when invoice already exists (IG-01)
        var f = CreateFixture();
        var existingInvoice = new Invoice
        {
            Id          = Guid.NewGuid(),
            ContractId  = f.Contract.Id,
            Contract    = f.Contract,   // needed: handler queries i.Contract!.Room!.BuildingId
            BillingYear = 2026,
            BillingMonth = 3,
            Status      = InvoiceStatus.Draft
        };
        SetupMocks(f, existingInvoices: [existingInvoice]);

        var result = await CreateHandler().Handle(
            new GenerateInvoicesCommand { BuildingId = f.Building.Id, BillingYear = 2026, BillingMonth = 3 }, default);

        result.Generated.Should().BeEmpty();
        result.Skipped.Should().HaveCount(1);
        result.Skipped[0].ContractId.Should().Be(f.Contract.Id);
    }

    [Fact]
    public async Task Handle_VoidedInvoiceExists_RegeneratesInvoice()
    {
        // IG-01: Voided invoices are excluded from idempotency check → re-generation allowed
        var f = CreateFixture();
        var voidedInvoice = new Invoice
        {
            Id           = Guid.NewGuid(),
            ContractId   = f.Contract.Id,
            Contract     = f.Contract,
            BillingYear  = 2026,
            BillingMonth = 3,
            Status       = InvoiceStatus.Void  // voided → should NOT block re-generation
        };
        SetupMocks(f, existingInvoices: [voidedInvoice]);

        var result = await CreateHandler().Handle(
            new GenerateInvoicesCommand { BuildingId = f.Building.Id, BillingYear = 2026, BillingMonth = 3 }, default);

        // Voided invoice not counted → new invoice generated
        result.Generated.Should().HaveCount(1);
        result.Skipped.Should().BeEmpty();
    }

    // ── @Service: Missing meter reading → warning (IG-02) ────────────────────

    [Fact]
    public async Task Handle_MeteredServiceWithNoReading_AddsWarningAndSkipsServiceLine()
    {
        // @Service: Show warning when meter reading is missing (IG-02)
        var f = CreateFixture();
        var electricity = new Service
        {
            Id         = Guid.NewGuid(),
            BuildingId = f.Building.Id,
            Name       = "Điện",
            IsMetered  = true,
            IsActive   = true,
            UnitPrice  = 3_500
        };
        SetupMocks(f, services: [electricity], meterReadings: []); // no reading

        var result = await CreateHandler().Handle(
            new GenerateInvoicesCommand { BuildingId = f.Building.Id, BillingYear = 2026, BillingMonth = 3 }, default);

        result.Generated.Should().HaveCount(1);
        result.Warnings.Should().Contain(w => w.Contains("Thiếu chỉ số đồng hồ") && w.Contains("Điện"));
        result.Generated[0].ServiceAmount.Should().Be(0); // electricity line skipped
    }

    // ── @Service: Skip disabled room service ──────────────────────────────────

    [Fact]
    public async Task Handle_ServiceDisabledForRoom_SkipsServiceLine()
    {
        // @Service: Skip service item when room service is disabled
        var f = CreateFixture();
        var water = new Service
        {
            Id         = Guid.NewGuid(),
            BuildingId = f.Building.Id,
            Name       = "Nước",
            IsMetered  = false,
            IsActive   = true,
            UnitPrice  = 100_000
        };
        var disabledOverride = new RoomService
        {
            RoomId    = f.Room.Id,
            Room      = f.Room,   // needed: handler queries rs.Room!.BuildingId
            ServiceId = water.Id,
            IsEnabled = false
        };
        SetupMocks(f, services: [water], roomServices: [disabledOverride]);

        var result = await CreateHandler().Handle(
            new GenerateInvoicesCommand { BuildingId = f.Building.Id, BillingYear = 2026, BillingMonth = 3 }, default);

        result.Generated.Should().HaveCount(1);
        result.Generated[0].ServiceAmount.Should().Be(0);
    }

    // ── @Service: Flat service with occupant count ────────────────────────────

    [Fact]
    public async Task Handle_FlatServiceWithThreeOccupants_CalculatesServiceAmountCorrectly()
    {
        // @Service: Calculate service amount using occupant count
        // 3 occupants × 100,000 = 300,000 → TotalAmount = 5,000,000 + 300,000 = 5,300,000
        var f = CreateFixture();
        var billingEnd = new DateOnly(2026, 3, 31);
        f.Contract.ContractTenants.Add(new ContractTenant { MoveInDate = new DateOnly(2026, 1, 1) });
        f.Contract.ContractTenants.Add(new ContractTenant { MoveInDate = new DateOnly(2026, 1, 1) });
        f.Contract.ContractTenants.Add(new ContractTenant { MoveInDate = new DateOnly(2026, 1, 1) });

        var internet = new Service
        {
            Id         = Guid.NewGuid(),
            BuildingId = f.Building.Id,
            Name       = "Internet",
            IsMetered  = false,
            IsActive   = true,
            UnitPrice  = 100_000
        };
        SetupMocks(f, services: [internet]);

        var result = await CreateHandler().Handle(
            new GenerateInvoicesCommand { BuildingId = f.Building.Id, BillingYear = 2026, BillingMonth = 3 }, default);

        result.Generated.Should().HaveCount(1);
        result.Generated[0].ServiceAmount.Should().Be(300_000);  // 3 × 100,000
        result.Generated[0].TotalAmount.Should().Be(5_300_000);  // 5M + 300k
    }

    // ── @Service: Override unit price for metered service ────────────────────

    [Fact]
    public async Task Handle_MeteredServiceWithOverrideUnitPrice_UsesOverridePrice()
    {
        // @Service: Override unit price takes priority over default service price
        // Consumption=10, default price=10,000, override price=15,000 → amount=150,000
        var f = CreateFixture();
        var water = new Service
        {
            Id         = Guid.NewGuid(),
            BuildingId = f.Building.Id,
            Name       = "Nước",
            IsMetered  = true,
            IsActive   = true,
            UnitPrice  = 10_000
        };
        var priceOverride = new RoomService
        {
            RoomId             = f.Room.Id,
            Room               = f.Room,
            ServiceId          = water.Id,
            IsEnabled          = true,
            OverrideUnitPrice  = 15_000
        };
        var reading = new MeterReading
        {
            RoomId       = f.Room.Id,
            Room         = f.Room,   // needed: handler queries mr.Room!.BuildingId
            ServiceId    = water.Id,
            BillingYear  = 2026,
            BillingMonth = 3,
            PreviousReading = 100,
            CurrentReading  = 110,
            Consumption     = 10
        };
        SetupMocks(f, services: [water], roomServices: [priceOverride], meterReadings: [reading]);

        var result = await CreateHandler().Handle(
            new GenerateInvoicesCommand { BuildingId = f.Building.Id, BillingYear = 2026, BillingMonth = 3 }, default);

        result.Generated.Should().HaveCount(1);
        result.Generated[0].ServiceAmount.Should().Be(150_000); // 10 × 15,000 (override price)
    }

    // ── @Service: Override quantity for flat service ──────────────────────────

    [Fact]
    public async Task Handle_FlatServiceWithOverrideQuantity_UsesOverrideQuantity()
    {
        // @Service: Override quantity takes priority over occupant count
        // 5 occupants but override qty=2, price=50,000 → amount=100,000
        var f = CreateFixture();
        for (var i = 0; i < 5; i++)
            f.Contract.ContractTenants.Add(new ContractTenant { MoveInDate = new DateOnly(2026, 1, 1) });

        var cleaning = new Service
        {
            Id         = Guid.NewGuid(),
            BuildingId = f.Building.Id,
            Name       = "Vệ sinh",
            IsMetered  = false,
            IsActive   = true,
            UnitPrice  = 50_000
        };
        var qtyOverride = new RoomService
        {
            RoomId            = f.Room.Id,
            Room              = f.Room,
            ServiceId         = cleaning.Id,
            IsEnabled         = true,
            OverrideQuantity  = 2   // override: 2, even though 5 occupants
        };
        SetupMocks(f, services: [cleaning], roomServices: [qtyOverride]);

        var result = await CreateHandler().Handle(
            new GenerateInvoicesCommand { BuildingId = f.Building.Id, BillingYear = 2026, BillingMonth = 3 }, default);

        result.Generated.Should().HaveCount(1);
        result.Generated[0].ServiceAmount.Should().Be(100_000); // 2 × 50,000 (override qty)
    }

    // ── @Service: Zero consumption creates line with zero amount ──────────────

    [Fact]
    public async Task Handle_MeteredServiceWithZeroConsumption_CreatesZeroAmountLine()
    {
        // @Service: Create service item with zero consumption → ServiceAmount = 0
        var f = CreateFixture();
        var electricity = new Service
        {
            Id         = Guid.NewGuid(),
            BuildingId = f.Building.Id,
            Name       = "Điện",
            IsMetered  = true,
            IsActive   = true,
            UnitPrice  = 3_500
        };
        var reading = new MeterReading
        {
            RoomId          = f.Room.Id,
            Room            = f.Room,
            ServiceId       = electricity.Id,
            BillingYear     = 2026,
            BillingMonth    = 3,
            PreviousReading = 1_000,
            CurrentReading  = 1_000,
            Consumption     = 0
        };
        SetupMocks(f, services: [electricity], meterReadings: [reading]);

        var result = await CreateHandler().Handle(
            new GenerateInvoicesCommand { BuildingId = f.Building.Id, BillingYear = 2026, BillingMonth = 3 }, default);

        result.Generated.Should().HaveCount(1);
        result.Generated[0].ServiceAmount.Should().Be(0);   // 0 × 3,500 = 0
        result.Warnings.Should().BeEmpty();                  // no warning (reading exists)
    }
}
