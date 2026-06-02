using Application.Common.Interfaces;
using Application.Features.Invoices.Commands;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using MockQueryable.Moq;
using Moq;
using Xunit;

namespace ElysStay.Tests.Unit.Business;
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
            MonthlyRent   = monthlyRent,
            StartDate     = new DateOnly(2026, 1, 1),
            EndDate       = new DateOnly(2027, 12, 31),
            MoveInDate    = new DateOnly(2026, 1, 1), // before billing period → no proration
            Status        = ContractStatus.Active,
            ContractTenants = new List<ContractTenant>
            {
                new() { TenantUserId = tenant.Id, IsMainTenant = true, Tenant = tenant, MoveInDate = new DateOnly(2026, 1, 1) }
            }
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
        var f = CreateFixture();
        var billingEnd = new DateOnly(2026, 3, 31);
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

    // ── @Service: RoomService overrides take priority ────────────────────────

    public record OverrideCase(
        bool     IsMetered,
        decimal  DefaultUnitPrice,
        decimal? OverrideUnitPrice,
        int?     OverrideQuantity,
        int      OccupantCount,
        decimal  Consumption,
        decimal  ExpectedServiceAmount);

    public static TheoryData<OverrideCase> OverrideCases => new()
    {
        // OverrideUnitPrice beats default: 10 × 15,000 = 150,000
        new OverrideCase(
            IsMetered:             true,
            DefaultUnitPrice:      10_000,
            OverrideUnitPrice:     15_000,
            OverrideQuantity:      null,
            OccupantCount:         0,
            Consumption:           10,
            ExpectedServiceAmount: 150_000),

        // OverrideQuantity beats occupant count: 2 × 50,000 = 100,000 (5 occupants ignored)
        new OverrideCase(
            IsMetered:             false,
            DefaultUnitPrice:      50_000,
            OverrideUnitPrice:     null,
            OverrideQuantity:      2,
            OccupantCount:         5,
            Consumption:           0,
            ExpectedServiceAmount: 100_000),
    };

    [Theory]
    [MemberData(nameof(OverrideCases))]
    public async Task Handle_RoomServiceOverrides_ShouldRespectPriorityRules(OverrideCase tc)
    {
        var f = CreateFixture();

        for (var i = 0; i < tc.OccupantCount; i++)
            f.Contract.ContractTenants.Add(new ContractTenant { MoveInDate = new DateOnly(2026, 1, 1) });

        var service = new Service
        {
            Id = Guid.NewGuid(),
            BuildingId = f.Building.Id,
            Name = "Service",
            IsMetered = tc.IsMetered,
            IsActive = true,
            UnitPrice = tc.DefaultUnitPrice,
        };

        var roomService = new RoomService
        {
            RoomId = f.Room.Id,
            Room = f.Room,
            ServiceId = service.Id,
            IsEnabled = true,
            OverrideUnitPrice = tc.OverrideUnitPrice,
            OverrideQuantity = tc.OverrideQuantity,
        };

        List<MeterReading> readings = tc.IsMetered
            ? [new MeterReading
              {
                  RoomId          = f.Room.Id,
                  Room            = f.Room,
                  ServiceId       = service.Id,
                  BillingYear     = 2026,
                  BillingMonth    = 3,
                  PreviousReading = 100,
                  CurrentReading  = 100 + tc.Consumption,
                  Consumption     = tc.Consumption,
              }]
            : [];

        SetupMocks(f, services: [service], roomServices: [roomService], meterReadings: readings);

        var result = await CreateHandler().Handle(
            new GenerateInvoicesCommand { BuildingId = f.Building.Id, BillingYear = 2026, BillingMonth = 3 }, default);

        result.Generated.Should().HaveCount(1);
        result.Generated[0].ServiceAmount.Should().Be(tc.ExpectedServiceAmount);
    }
}
