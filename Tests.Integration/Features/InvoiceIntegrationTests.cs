using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Invoices.Commands;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Moq;
using Tests.Integration.Builders;
using Tests.Integration.Fixtures;
using Xunit;

namespace ElysStay.Tests.Integration.Features;

/// <summary>
/// Integration tests for GenerateInvoicesCommandHandler.
/// Uses a real PostgreSQL container (Testcontainers) and the real ApplicationDbContext.
/// Only ICurrentUserService is mocked — all other dependencies are real.
/// Each test gets a fresh database to avoid state bleed.
/// </summary>
public class InvoiceGenerationIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();

    // Shared test entities (seeded in SetupBaseData)
    private User     _owner   = null!;
    private Building _building = null!;
    private Room     _room    = null!;
    private User     _tenant  = null!;
    private Contract _contract = null!;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync()    => await _fixture.DisposeAsync();

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Seed the minimum required entities (owner, building, room, tenant, contract).</summary>
    private async Task SetupBaseData(
        DateOnly? moveInDate = null,
        DateOnly? startDate  = null,
        decimal   rent       = 5_000_000)
    {
        _owner    = TestDataBuilder.CreateUser(role: UserRole.Owner);
        _tenant   = TestDataBuilder.CreateUser(role: UserRole.Tenant);
        _building = TestDataBuilder.CreateBuilding(_owner.Id);
        _room     = TestDataBuilder.CreateRoom(_building.Id);
        _contract = TestDataBuilder.CreateContract(
            _room.Id, _tenant.Id, _owner.Id,
            monthlyRent: rent,
            MoveInDate: moveInDate ?? new DateOnly(2026, 1, 1),
            StartDate:  startDate  ?? new DateOnly(2026, 1, 1));

        _fixture.DbContext.Users.AddRange(_owner, _tenant);
        _fixture.DbContext.Buildings.Add(_building);
        _fixture.DbContext.Rooms.Add(_room);
        _fixture.DbContext.Contracts.Add(_contract);
        await _fixture.DbContext.SaveChangesAsync();
    }

    /// <summary>Build the handler under test with real DB + owner auth mock.</summary>
    private GenerateInvoicesCommandHandler CreateHandler()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(m => m.GetRequiredUserId()).Returns(_owner.Id);
        currentUser.Setup(m => m.IsOwner).Returns(true);
        currentUser.Setup(m => m.Role).Returns(UserRole.Owner);

        // Real BuildingScopeService: queries DB to verify OwnerId == userId
        var buildingScope = new BuildingScopeService(_fixture.DbContext, currentUser.Object);

        return new GenerateInvoicesCommandHandler(
            _fixture.DbContext, currentUser.Object, buildingScope);
    }

    private GenerateInvoicesCommand Command(int year = 2026, int month = 3)
        => new() { BuildingId = _building.Id, BillingYear = year, BillingMonth = month };

    // ── @Invoice: Generate invoice successfully (IG-07) ───────────────────────

    [Fact]
    public async Task GenerateInvoices_FullMonthActiveContract_CreatesOneDraftInvoice()
    {
        await SetupBaseData(moveInDate: new DateOnly(2026, 1, 1));

        var result = await CreateHandler().Handle(Command(2026, 3), default);

        // 1 contract → 1 generated, nothing skipped
        result.Generated.Should().HaveCount(1);
        result.Skipped.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();

        // IG-07: status must be Draft
        var invoice = await _fixture.DbContext.Invoices
            .FirstAsync(i => i.ContractId == _contract.Id);
        invoice.Status.Should().Be(InvoiceStatus.Draft);
        invoice.RentAmount.Should().Be(5_000_000);
        invoice.TotalAmount.Should().Be(5_000_000);
    }

    [Fact]
    public async Task GenerateInvoices_DueDate_IsNextMonthInvoiceDueDay()
    {
        // Building.InvoiceDueDay = 10 → DueDate = April 10, 2026
        await SetupBaseData();

        await CreateHandler().Handle(Command(2026, 3), default);

        var invoice = await _fixture.DbContext.Invoices.FirstAsync(i => i.ContractId == _contract.Id);
        invoice.DueDate.Should().Be(new DateOnly(2026, 4, 10)); // IG-06
    }

    // ── @Invoice: Idempotency (IG-01) ─────────────────────────────────────────

    [Fact]
    public async Task GenerateInvoices_InvoiceAlreadyExists_SkipsContractAndCreatesNoDuplicate()
    {
        await SetupBaseData();

        // Pre-existing invoice for the same contract + period
        var existing = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id, billingMonth: 3, billingYear: 2026);
        _fixture.DbContext.Invoices.Add(existing);
        await _fixture.DbContext.SaveChangesAsync();

        var result = await CreateHandler().Handle(Command(2026, 3), default);

        result.Generated.Should().BeEmpty();
        result.Skipped.Should().HaveCount(1);
        result.Skipped[0].ContractId.Should().Be(_contract.Id);

        // DB should still have only 1 invoice (no duplicate)
        var count = await _fixture.DbContext.Invoices
            .CountAsync(i => i.ContractId == _contract.Id && i.BillingYear == 2026 && i.BillingMonth == 3);
        count.Should().Be(1);
    }

    [Fact]
    public async Task GenerateInvoices_VoidedInvoiceExists_AllowsRegeneration()
    {
        // IG-01: voided invoices are excluded from the idempotency check
        await SetupBaseData();

        var voided = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id,
            billingMonth: 3, billingYear: 2026, status: InvoiceStatus.Void);
        _fixture.DbContext.Invoices.Add(voided);
        await _fixture.DbContext.SaveChangesAsync();

        var result = await CreateHandler().Handle(Command(2026, 3), default);

        result.Generated.Should().HaveCount(1);
        result.Skipped.Should().BeEmpty();

        // 2 invoices now: 1 voided + 1 new Draft
        var invoices = await _fixture.DbContext.Invoices
            .Where(i => i.ContractId == _contract.Id && i.BillingYear == 2026 && i.BillingMonth == 3)
            .ToListAsync();
        invoices.Should().HaveCount(2);
        invoices.Should().ContainSingle(i => i.Status == InvoiceStatus.Draft);
        invoices.Should().ContainSingle(i => i.Status == InvoiceStatus.Void);
    }

    // ── @Rent: Proration ──────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateInvoices_ContractStartsMidMonth_ProratesRentCorrectly()
    {
        // Contract MoveInDate = March 16 → 16 days → Round(5_000_000 / 31 * 16) = 2_580_645
        await SetupBaseData(moveInDate: new DateOnly(2026, 3, 16), startDate: new DateOnly(2026, 3, 16));

        var result = await CreateHandler().Handle(Command(2026, 3), default);

        result.Generated.Should().HaveCount(1);

        var invoice = await _fixture.DbContext.Invoices.FirstAsync(i => i.ContractId == _contract.Id);
        invoice.RentAmount.Should().Be(2_580_645);
    }

    [Fact]
    public async Task GenerateInvoices_ContractTerminatedMidMonth_ProratesRentCorrectly()
    {
        // TerminationDate = March 10 → 10 days → Round(5_000_000 / 31 * 10) = 1_612_903
        await SetupBaseData();
        _contract.TerminationDate = new DateOnly(2026, 3, 10);
        await _fixture.DbContext.SaveChangesAsync();

        var result = await CreateHandler().Handle(Command(2026, 3), default);

        result.Generated.Should().HaveCount(1);

        var invoice = await _fixture.DbContext.Invoices.FirstAsync(i => i.ContractId == _contract.Id);
        invoice.RentAmount.Should().Be(1_612_903);
    }

    // ── @Service: Metered service (IG-03) ─────────────────────────────────────

    [Fact]
    public async Task GenerateInvoices_MeteredServiceWithReading_CreatesServiceLineItem()
    {
        // Water service: consumption 10 m³ × 10,000 = 100,000
        await SetupBaseData();

        var water = TestDataBuilder.CreateService(_building.Id, "Nước", "m³", unitPrice: 10_000, isMetered: true);
        var reading = TestDataBuilder.CreateMeterReading(
            _room.Id, water.Id, _owner.Id,
            billingMonth: 3, billingYear: 2026,
            previousReading: 100, currentReading: 110);

        _fixture.DbContext.Services.Add(water);
        _fixture.DbContext.MeterReadings.Add(reading);
        await _fixture.DbContext.SaveChangesAsync();

        var result = await CreateHandler().Handle(Command(2026, 3), default);

        result.Generated.Should().HaveCount(1);
        result.Warnings.Should().BeEmpty();

        var invoice = await _fixture.DbContext.Invoices
            .Include(i => i.InvoiceDetails)
            .FirstAsync(i => i.ContractId == _contract.Id);

        invoice.ServiceAmount.Should().Be(100_000);              // 10 × 10,000
        invoice.TotalAmount.Should().Be(5_100_000);              // 5,000,000 + 100,000

        var waterLine = invoice.InvoiceDetails.First(d => d.ServiceId == water.Id);
        waterLine.Quantity.Should().Be(10);
        waterLine.UnitPrice.Should().Be(10_000);
        waterLine.Amount.Should().Be(100_000);
        waterLine.PreviousReading.Should().Be(100);
        waterLine.CurrentReading.Should().Be(110);
    }

    // ── @Service: Missing meter reading → warning (IG-02) ────────────────────

    [Fact]
    public async Task GenerateInvoices_MeteredServiceWithNoReading_AddsWarningAndStillCreatesInvoice()
    {
        await SetupBaseData();

        var electricity = TestDataBuilder.CreateService(
            _building.Id, "Điện", "kWh", unitPrice: 3_500, isMetered: true);
        _fixture.DbContext.Services.Add(electricity);
        await _fixture.DbContext.SaveChangesAsync();
        // No meter reading added

        var result = await CreateHandler().Handle(Command(2026, 3), default);

        // Invoice still created (IG-02: skip service line only, not the whole invoice)
        result.Generated.Should().HaveCount(1);
        result.Warnings.Should().ContainSingle(w => w.Contains("Thiếu chỉ số đồng hồ") && w.Contains("Điện"));

        var invoice = await _fixture.DbContext.Invoices.FirstAsync(i => i.ContractId == _contract.Id);
        invoice.ServiceAmount.Should().Be(0);         // electricity line skipped
        invoice.TotalAmount.Should().Be(5_000_000);   // rent only
    }

    // ── @Service: Flat service with occupant count (IG-04) ────────────────────

    [Fact]
    public async Task GenerateInvoices_FlatServiceWithOccupants_CalculatesServiceAmountCorrectly()
    {
        // Internet: 3 occupants × 100,000 = 300,000
        await SetupBaseData();

        _contract.ContractTenants.Add(new ContractTenant { MoveInDate = new DateOnly(2026, 1, 1), TenantUserId = _tenant.Id });
        _contract.ContractTenants.Add(new ContractTenant { MoveInDate = new DateOnly(2026, 1, 1), TenantUserId = Guid.NewGuid() });
        _contract.ContractTenants.Add(new ContractTenant { MoveInDate = new DateOnly(2026, 1, 1), TenantUserId = Guid.NewGuid() });

        var internet = TestDataBuilder.CreateService(
            _building.Id, "Internet", "người", unitPrice: 100_000, isMetered: false);
        _fixture.DbContext.Services.Add(internet);
        await _fixture.DbContext.SaveChangesAsync();

        var result = await CreateHandler().Handle(Command(2026, 3), default);

        result.Generated.Should().HaveCount(1);

        var invoice = await _fixture.DbContext.Invoices.FirstAsync(i => i.ContractId == _contract.Id);
        invoice.ServiceAmount.Should().Be(300_000);     // 3 × 100,000
        invoice.TotalAmount.Should().Be(5_300_000);     // 5,000,000 + 300,000
    }

    // ── @Service: Override unit price (IG-03) ────────────────────────────────

    [Fact]
    public async Task GenerateInvoices_MeteredServiceWithOverridePrice_UsesOverridePrice()
    {
        // Water: default 10,000 → override 15,000. Consumption=10 → amount=150,000
        await SetupBaseData();

        var water = TestDataBuilder.CreateService(_building.Id, "Nước", "m³", unitPrice: 10_000, isMetered: true);
        var priceOverride = new RoomService
        {
            RoomId            = _room.Id,
            ServiceId         = water.Id,
            IsEnabled         = true,
            OverrideUnitPrice = 15_000
        };
        var reading = TestDataBuilder.CreateMeterReading(
            _room.Id, water.Id, _owner.Id, 3, 2026, previousReading: 100, currentReading: 110);

        _fixture.DbContext.Services.Add(water);
        _fixture.DbContext.Set<RoomService>().Add(priceOverride);
        _fixture.DbContext.MeterReadings.Add(reading);
        await _fixture.DbContext.SaveChangesAsync();

        var result = await CreateHandler().Handle(Command(2026, 3), default);

        result.Generated.Should().HaveCount(1);

        var invoice = await _fixture.DbContext.Invoices
            .Include(i => i.InvoiceDetails)
            .FirstAsync(i => i.ContractId == _contract.Id);

        var waterLine = invoice.InvoiceDetails.First(d => d.ServiceId == water.Id);
        waterLine.UnitPrice.Should().Be(15_000);          // override price used
        waterLine.Amount.Should().Be(150_000);            // 10 × 15,000
    }

    // ── @Service: Disabled room service → skip ────────────────────────────────

    [Fact]
    public async Task GenerateInvoices_ServiceDisabledForRoom_ServiceLineNotIncluded()
    {
        await SetupBaseData();

        var water = TestDataBuilder.CreateService(_building.Id, "Nước", "m³", unitPrice: 10_000, isMetered: false);
        var disabledOverride = new RoomService
        {
            RoomId    = _room.Id,
            ServiceId = water.Id,
            IsEnabled = false   // disabled for this room
        };

        _fixture.DbContext.Services.Add(water);
        _fixture.DbContext.Set<RoomService>().Add(disabledOverride);
        await _fixture.DbContext.SaveChangesAsync();

        var result = await CreateHandler().Handle(Command(2026, 3), default);

        result.Generated.Should().HaveCount(1);

        var invoice = await _fixture.DbContext.Invoices
            .Include(i => i.InvoiceDetails)
            .FirstAsync(i => i.ContractId == _contract.Id);

        invoice.ServiceAmount.Should().Be(0);
        invoice.InvoiceDetails.Should().NotContain(d => d.ServiceId == water.Id);
    }

    // ── @Invoice: Multiple contracts → one invoice each ───────────────────────

    [Fact]
    public async Task GenerateInvoices_TwoActiveContracts_GeneratesTwoInvoices()
    {
        await SetupBaseData();

        // Second room + contract in the same building
        var room2     = TestDataBuilder.CreateRoom(_building.Id, roomNumber: "102");
        var tenant2   = TestDataBuilder.CreateUser(role: UserRole.Tenant);
        var contract2 = TestDataBuilder.CreateContract(
            room2.Id, tenant2.Id, _owner.Id,
            MoveInDate: new DateOnly(2026, 1, 1),
            StartDate:  new DateOnly(2026, 1, 1));

        _fixture.DbContext.Users.Add(tenant2);
        _fixture.DbContext.Rooms.Add(room2);
        _fixture.DbContext.Contracts.Add(contract2);
        await _fixture.DbContext.SaveChangesAsync();

        var result = await CreateHandler().Handle(Command(2026, 3), default);

        result.Generated.Should().HaveCount(2);
        result.Skipped.Should().BeEmpty();

        var invoiceCount = await _fixture.DbContext.Invoices
            .CountAsync(i => i.BillingYear == 2026 && i.BillingMonth == 3);
        invoiceCount.Should().Be(2);
    }

    // ── @Invoice: Contract terminated before period → skip ────────────────────

    [Fact]
    public async Task GenerateInvoices_ContractTerminatedBeforePeriod_SkipsContract()
    {
        await SetupBaseData();
        _contract.TerminationDate = new DateOnly(2026, 2, 28); // terminated before March
        await _fixture.DbContext.SaveChangesAsync();

        var result = await CreateHandler().Handle(Command(2026, 3), default);

        result.Generated.Should().BeEmpty();
        result.Skipped.Should().HaveCount(1);

        var invoiceCount = await _fixture.DbContext.Invoices.CountAsync();
        invoiceCount.Should().Be(0);
    }

    // ── @Invoice: Contract starts after period → skip ─────────────────────────

    [Fact]
    public async Task GenerateInvoices_ContractStartsAfterPeriod_SkipsContract()
    {
        // Contract starts April 1 → should be skipped for March billing
        await SetupBaseData(startDate: new DateOnly(2026, 4, 1), moveInDate: new DateOnly(2026, 4, 1));

        var result = await CreateHandler().Handle(Command(2026, 3), default);

        result.Generated.Should().BeEmpty();
        result.Skipped.Should().HaveCount(1);
    }

    // ── Authorization ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateInvoices_UserDoesNotOwnBuilding_ThrowsForbiddenException()
    {
        await SetupBaseData();

        // Different user trying to generate for this building
        var otherUser = TestDataBuilder.CreateUser(role: UserRole.Owner);
        _fixture.DbContext.Users.Add(otherUser);
        await _fixture.DbContext.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(m => m.GetRequiredUserId()).Returns(otherUser.Id);
        currentUser.Setup(m => m.IsOwner).Returns(true);
        currentUser.Setup(m => m.Role).Returns(UserRole.Owner);

        var buildingScope = new BuildingScopeService(_fixture.DbContext, currentUser.Object);
        var handler = new GenerateInvoicesCommandHandler(
            _fixture.DbContext, currentUser.Object, buildingScope);

        var act = () => handler.Handle(Command(2026, 3), default);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    // ── EF Persistence ────────────────────────────────────────────────────────
    // These tests verify that EF Core correctly persists direct status mutations
    // and that basic LINQ filters work against the real schema.

    [Fact]
    public async Task UpdateInvoiceStatus_ToPartialPaid_UpdatesSuccessfully()
    {
        await SetupBaseData();
        var invoice = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id);
        _fixture.DbContext.Invoices.Add(invoice);
        await _fixture.DbContext.SaveChangesAsync();

        invoice.Status = InvoiceStatus.PartiallyPaid;
        _fixture.DbContext.Invoices.Update(invoice);
        await _fixture.DbContext.SaveChangesAsync();

        var updated = await _fixture.DbContext.Invoices.FindAsync(invoice.Id);
        updated!.Status.Should().Be(InvoiceStatus.PartiallyPaid);
    }

    [Fact]
    public async Task VoidInvoice_WithValidInvoice_PersistsVoidStatus()
    {
        await SetupBaseData();
        var invoice = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id);
        _fixture.DbContext.Invoices.Add(invoice);
        await _fixture.DbContext.SaveChangesAsync();

        invoice.Status = InvoiceStatus.Void;
        _fixture.DbContext.Invoices.Update(invoice);
        await _fixture.DbContext.SaveChangesAsync();

        var voided = await _fixture.DbContext.Invoices.FindAsync(invoice.Id);
        voided!.Status.Should().Be(InvoiceStatus.Void);
    }

    [Fact]
    public async Task GetInvoices_FiltersByContract_ReturnsOnlyContractInvoices()
    {
        await SetupBaseData();
        var invoice1 = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id, billingMonth: 3);
        var invoice2 = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id, billingMonth: 4);
        _fixture.DbContext.Invoices.AddRange(invoice1, invoice2);
        await _fixture.DbContext.SaveChangesAsync();

        var invoices = _fixture.DbContext.Invoices
            .Where(i => i.ContractId == _contract.Id)
            .ToList();

        invoices.Should().HaveCount(2);
        invoices.Should().AllSatisfy(i => i.ContractId.Should().Be(_contract.Id));
    }
}
