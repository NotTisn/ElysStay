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
public class InvoiceIntegrationTests : IAsyncLifetime
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


    // ── @Invoice: Contract don't active in period  ─────────────────────────
    [Theory]
        [InlineData("StatusTerminated")]
        [InlineData("TerminatedBeforePeriod")]
        [InlineData("StartsAfterPeriod")]
        public async Task GenerateInvoices_IneligibleContract_DoesNotGenerateInvoice(string scenario)
        {
            await SetupBaseData();

            switch (scenario)
            {
                case "StatusTerminated":
                    _contract.Status = ContractStatus.Terminated;
                    break;

                case "TerminatedBeforePeriod":
                    _contract.TerminationDate = new DateOnly(2026, 2, 28);
                    break;

                case "StartsAfterPeriod":
                    _contract.StartDate = new DateOnly(2026, 4, 1);
                    _contract.MoveInDate = new DateOnly(2026, 4, 1);
                    break;
            }

            await _fixture.DbContext.SaveChangesAsync();

            var result = await CreateHandler().Handle(Command(2026, 3), default);

            // A contract that is not active within the billing period is filtered out of candidacy
            // entirely: no invoice is generated. `Skipped` is reserved for contracts that ARE
            // candidates but already have an invoice for the period, so it stays empty here.
            result.Generated.Should().BeEmpty();
            result.Skipped.Should().BeEmpty();

            // Scope the count to this contract — the shared test database is not reset between
            // tests, so a global Invoices count would include rows created by other tests.
            var invoiceCount = await _fixture.DbContext.Invoices
                .CountAsync(i => i.ContractId == _contract.Id);
            invoiceCount.Should().Be(0);
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


    // ── @Service: Mix of metered + flat services ──────────────────────────────

    [Fact]
    public async Task GenerateInvoices_MixedMeteredAndFlatServices_BothLineItemsCreated()
    {
        // Electricity (metered): 10 kWh × 3,500 = 35,000
        // Internet (flat):        1 occupant × 100,000 = 100,000
        // ServiceAmount = 135,000 | TotalAmount = 5,135,000
        await SetupBaseData();

        // _tenant đã có sẵn trong ContractTenants từ SetupBaseData (1 occupant) → không thêm lại
        var electricity = TestDataBuilder.CreateService(_building.Id, "Điện", "kWh", unitPrice: 3_500, isMetered: true);
        var internet    = TestDataBuilder.CreateService(_building.Id, "Internet", "người", unitPrice: 100_000, isMetered: false);
        var reading     = TestDataBuilder.CreateMeterReading(_room.Id, electricity.Id, _owner.Id, 3, 2026,
                              previousReading: 200, currentReading: 210);

        _fixture.DbContext.Services.AddRange(electricity, internet);
        _fixture.DbContext.MeterReadings.Add(reading);
        await _fixture.DbContext.SaveChangesAsync();

        var result = await CreateHandler().Handle(Command(2026, 3), default);

        result.Generated.Should().HaveCount(1);
        result.Warnings.Should().BeEmpty();

        var invoice = await _fixture.DbContext.Invoices
            .Include(i => i.InvoiceDetails)
            .FirstAsync(i => i.ContractId == _contract.Id);

        invoice.ServiceAmount.Should().Be(135_000);
        invoice.TotalAmount.Should().Be(5_135_000);
        invoice.InvoiceDetails.Should().HaveCount(3); // rent + electricity + internet
        invoice.InvoiceDetails.Should().Contain(d => d.ServiceId == electricity.Id && d.Amount == 35_000);
        invoice.InvoiceDetails.Should().Contain(d => d.ServiceId == internet.Id    && d.Amount == 100_000);
    }

    // ── @Service: Zero active occupants → warning + skip (IG-04) ─────────────

    [Fact]
    public async Task GenerateInvoices_FlatServiceWithZeroOccupants_AddsWarningAndSkipsServiceLine()
    {
        // activeOccupantCount = 0 khi tenant move out trước billing period (March 2026)
        await SetupBaseData();

        _contract.ContractTenants.First().MoveOutDate = new DateOnly(2026, 2, 28);

        var internet = TestDataBuilder.CreateService(_building.Id, "Internet", "người", unitPrice: 100_000, isMetered: false);
        _fixture.DbContext.Services.Add(internet);
        await _fixture.DbContext.SaveChangesAsync();

        var result = await CreateHandler().Handle(Command(2026, 3), default);

        result.Generated.Should().HaveCount(1);
        result.Warnings.Should().ContainSingle(w => w.Contains("Không có cư dân") && w.Contains("Internet"));

        var invoice = await _fixture.DbContext.Invoices.FirstAsync(i => i.ContractId == _contract.Id);
        invoice.ServiceAmount.Should().Be(0);
        invoice.TotalAmount.Should().Be(5_000_000);
    }


    // ── @Invoice: InvoiceDetails are persisted and linked ─────────────────────

    [Fact]
    public async Task GenerateInvoices_WithMeteredService_InvoiceDetailsArePersistedAndLinked()
    {
        // Water: 15 consumption (50→65) × 10,000 = 150,000
        await SetupBaseData();

        var water   = TestDataBuilder.CreateService(_building.Id, "Nước", "m³", unitPrice: 10_000, isMetered: true);
        var reading = TestDataBuilder.CreateMeterReading(_room.Id, water.Id, _owner.Id, 3, 2026,
                          previousReading: 50, currentReading: 65);
        _fixture.DbContext.Services.Add(water);
        _fixture.DbContext.MeterReadings.Add(reading);
        await _fixture.DbContext.SaveChangesAsync();

        await CreateHandler().Handle(Command(2026, 3), default);

        var invoice = await _fixture.DbContext.Invoices
            .Include(i => i.InvoiceDetails)
            .FirstAsync(i => i.ContractId == _contract.Id);

        invoice.InvoiceDetails.Should().HaveCount(2); // rent + water
        invoice.InvoiceDetails.Should().AllSatisfy(d => d.InvoiceId.Should().Be(invoice.Id));

        var rentLine  = invoice.InvoiceDetails.First(d => d.ServiceId == null);
        var waterLine = invoice.InvoiceDetails.First(d => d.ServiceId == water.Id);

        rentLine.Amount.Should().Be(5_000_000);
        waterLine.Amount.Should().Be(150_000);         // 15 × 10,000
        waterLine.PreviousReading.Should().Be(50);
        waterLine.CurrentReading.Should().Be(65);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // UpdateInvoice integration tests
    // ══════════════════════════════════════════════════════════════════════════

    private UpdateInvoiceCommandHandler CreateUpdateHandler()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(m => m.GetRequiredUserId()).Returns(_owner.Id);
        currentUser.Setup(m => m.IsOwner).Returns(true);
        currentUser.Setup(m => m.Role).Returns(UserRole.Owner);
        var buildingScope = new BuildingScopeService(_fixture.DbContext, currentUser.Object);
        return new UpdateInvoiceCommandHandler(_fixture.DbContext, currentUser.Object, buildingScope);
    }

    // ── @Adjustment: Penalty / discount / note ───────────────────────────────

    [Fact]
    public async Task UpdateInvoice_AddPenalty_RecalculatesTotalAndPersists()
    {
        await SetupBaseData();
        var invoice = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id, rentAmount: 5_000_000);
        _fixture.DbContext.Invoices.Add(invoice);
        await _fixture.DbContext.SaveChangesAsync();

        var result = await CreateUpdateHandler().Handle(
            new UpdateInvoiceCommand { Id = invoice.Id, PenaltyAmount = 300_000 }, default);

        result.TotalAmount.Should().Be(5_300_000);
        result.PenaltyAmount.Should().Be(300_000);

        var persisted = await _fixture.DbContext.Invoices.FindAsync(invoice.Id);
        persisted!.TotalAmount.Should().Be(5_300_000);
        persisted.PenaltyAmount.Should().Be(300_000);
    }

    [Fact]
    public async Task UpdateInvoice_AddDiscount_RecalculatesTotalAndPersists()
    {
        await SetupBaseData();
        var invoice = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id, rentAmount: 5_000_000);
        _fixture.DbContext.Invoices.Add(invoice);
        await _fixture.DbContext.SaveChangesAsync();

        var result = await CreateUpdateHandler().Handle(
            new UpdateInvoiceCommand { Id = invoice.Id, DiscountAmount = 500_000 }, default);

        result.TotalAmount.Should().Be(4_500_000);

        var persisted = await _fixture.DbContext.Invoices.FindAsync(invoice.Id);
        persisted!.TotalAmount.Should().Be(4_500_000);
        persisted.DiscountAmount.Should().Be(500_000);
    }

    [Fact]
    public async Task UpdateInvoice_UpdateNote_PersistsNote()
    {
        await SetupBaseData();
        var invoice = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id);
        _fixture.DbContext.Invoices.Add(invoice);
        await _fixture.DbContext.SaveChangesAsync();

        await CreateUpdateHandler().Handle(
            new UpdateInvoiceCommand { Id = invoice.Id, Note = "Khách làm hỏng đồ" }, default);

        var persisted = await _fixture.DbContext.Invoices.FindAsync(invoice.Id);
        persisted!.Note.Should().Be("Khách làm hỏng đồ");
    }

    [Fact]
    public async Task UpdateInvoice_PenaltyAndDiscountTogether_RecalculatesTotalCorrectly()
    {
        // rent=5M + service=0 + penalty=500k - discount=100k = 5,400,000
        await SetupBaseData();
        var invoice = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id, rentAmount: 5_000_000);
        _fixture.DbContext.Invoices.Add(invoice);
        await _fixture.DbContext.SaveChangesAsync();

        var result = await CreateUpdateHandler().Handle(
            new UpdateInvoiceCommand { Id = invoice.Id, PenaltyAmount = 500_000, DiscountAmount = 100_000 }, default);

        result.TotalAmount.Should().Be(5_400_000);

        var persisted = await _fixture.DbContext.Invoices.FindAsync(invoice.Id);
        persisted!.TotalAmount.Should().Be(5_400_000);
    }

    [Fact]
    public async Task UpdateInvoice_WhenInvoiceNotFound_ThrowsNotFoundException()
    {
        await SetupBaseData();

        var act = () => CreateUpdateHandler().Handle(
            new UpdateInvoiceCommand { Id = Guid.NewGuid(), PenaltyAmount = 100_000 }, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
