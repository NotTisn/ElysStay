using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Contracts.Commands;
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
/// Integration tests for TerminateContractCommandHandler (move-out process).
/// Uses real PostgreSQL (Testcontainers) + real ApplicationDbContext.
/// ICurrentUserService and IEmailService are mocked.
/// Each test resets the database to avoid state bleed.
/// </summary>
public class TerminateContractIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();

    private User     _owner    = null!;
    private Building _building = null!;
    private Room     _room     = null!;
    private User     _tenant   = null!;
    private Contract _contract = null!;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync()    => await _fixture.DisposeAsync();

    // ── Handler factory ────────────────────────────────────────────────────────

    private TerminateContractCommandHandler CreateHandler(Guid? ownerId = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(m => m.GetRequiredUserId()).Returns(ownerId ?? _owner.Id);
        currentUser.Setup(m => m.IsOwner).Returns(true);

        var emailService = new Mock<IEmailService>();
        emailService
            .Setup(m => m.TrySendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var buildingScope = new BuildingScopeService(_fixture.DbContext, currentUser.Object);

        return new TerminateContractCommandHandler(
            _fixture.DbContext, currentUser.Object, buildingScope, emailService.Object);
    }

    // ── Seed helpers ───────────────────────────────────────────────────────────

    private async Task SeedBaseData(
        decimal deposit = 10_000_000,
        RoomStatus roomStatus = RoomStatus.Occupied)
    {
        _owner    = TestDataBuilder.CreateUser(role: UserRole.Owner);
        _tenant   = TestDataBuilder.CreateUser(role: UserRole.Tenant);
        _building = TestDataBuilder.CreateBuilding(_owner.Id);
        _room     = TestDataBuilder.CreateRoom(_building.Id);
        _room.Status = roomStatus;

        _contract = TestDataBuilder.CreateContract(
            _room.Id, _tenant.Id, _owner.Id,
            depositAmount: deposit,
            StartDate:  new DateOnly(2026, 1, 1),
            MoveInDate: new DateOnly(2026, 1, 1),
            EndDate:    new DateOnly(2027, 12, 31));

        _fixture.DbContext.Users.AddRange(_owner, _tenant);
        _fixture.DbContext.Buildings.Add(_building);
        _fixture.DbContext.Rooms.Add(_room);
        _fixture.DbContext.Contracts.Add(_contract);
        await _fixture.DbContext.SaveChangesAsync();
    }

    private async Task AddContractTenant(DateOnly? moveOutDate = null)
    {
        var occupant = TestDataBuilder.CreateUser(role: UserRole.Tenant);
        _fixture.DbContext.Users.Add(occupant);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ContractTenants.Add(new ContractTenant
        {
            Id           = Guid.NewGuid(),
            ContractId   = _contract.Id,
            TenantUserId = occupant.Id,
            IsMainTenant = false,
            MoveInDate   = new DateOnly(2026, 1, 1),
            MoveOutDate  = moveOutDate
        });
        await _fixture.DbContext.SaveChangesAsync();
    }

    private async Task AddFutureInvoice(
        int billingYear, int billingMonth,
        InvoiceStatus status = InvoiceStatus.Draft,
        bool withPayment = false)
    {
        var invoice = TestDataBuilder.CreateInvoice(
            _contract.Id, _owner.Id, billingMonth, billingYear, status: status);
        _fixture.DbContext.Invoices.Add(invoice);
        await _fixture.DbContext.SaveChangesAsync();

        if (withPayment)
        {
            var payment = TestDataBuilder.CreatePayment(invoice.Id, _owner.Id, 1_000_000);
            _fixture.DbContext.Payments.Add(payment);
            await _fixture.DbContext.SaveChangesAsync();
        }
    }

    // ── DEP-04: Deposit calculation and DepositStatus ─────────────────────────

    [Fact]
    public async Task Terminate_NoDeductions_CreatesFullRefundPaymentAndSetsRefunded()
    {
        await SeedBaseData(deposit: 10_000_000);

        var result = await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = _contract.Id,
            TerminationDate = new DateOnly(2026, 6, 30),
            Deductions = 0
        }, default);

        result.RefundAmount.Should().Be(10_000_000);
        result.DepositStatus.Should().Be(DepositStatus.Refunded.ToString());

        var dbContract = await _fixture.DbContext.Contracts.FindAsync(_contract.Id);
        dbContract!.DepositStatus.Should().Be(DepositStatus.Refunded);
        dbContract.RefundAmount.Should().Be(10_000_000);

        var refundPayment = await _fixture.DbContext.Payments
            .FirstOrDefaultAsync(p => p.ContractId == _contract.Id && p.Type == PaymentType.DepositRefund);
        refundPayment.Should().NotBeNull("a DEPOSIT_REFUND payment must be created");
        refundPayment!.Amount.Should().Be(10_000_000);
    }

    [Fact]
    public async Task Terminate_PartialDeductions_CreatesPartialRefundPaymentAndSetsPartiallyRefunded()
    {
        await SeedBaseData(deposit: 10_000_000);

        var result = await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = _contract.Id,
            TerminationDate = new DateOnly(2026, 6, 30),
            Deductions = 3_000_000
        }, default);

        result.RefundAmount.Should().Be(7_000_000);
        result.DepositStatus.Should().Be(DepositStatus.PartiallyRefunded.ToString());

        var dbContract = await _fixture.DbContext.Contracts.FindAsync(_contract.Id);
        dbContract!.DepositStatus.Should().Be(DepositStatus.PartiallyRefunded);
        dbContract.RefundAmount.Should().Be(7_000_000);

        var refundPayment = await _fixture.DbContext.Payments
            .FirstOrDefaultAsync(p => p.ContractId == _contract.Id && p.Type == PaymentType.DepositRefund);
        refundPayment.Should().NotBeNull();
        refundPayment!.Amount.Should().Be(7_000_000);
    }

    [Fact]
    public async Task Terminate_FullDeductions_SetsDepositForfeitedAndCreatesAuditPayment()
    {
        await SeedBaseData(deposit: 10_000_000);

        var result = await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = _contract.Id,
            TerminationDate = new DateOnly(2026, 6, 30),
            Deductions = 10_000_000
        }, default);

        result.RefundAmount.Should().Be(0);
        result.DepositStatus.Should().Be(DepositStatus.Forfeited.ToString());

        var dbContract = await _fixture.DbContext.Contracts.FindAsync(_contract.Id);
        dbContract!.DepositStatus.Should().Be(DepositStatus.Forfeited);
        dbContract.RefundAmount.Should().Be(0);

        // Audit trail payment must still be created even for full forfeit (DEP-04)
        var auditPayment = await _fixture.DbContext.Payments
            .FirstOrDefaultAsync(p => p.ContractId == _contract.Id && p.Type == PaymentType.DepositRefund);
        auditPayment.Should().NotBeNull("forfeit must still produce an audit trail payment");
        auditPayment!.Amount.Should().Be(0);
    }

    // ── SM-10 / SM-04: State machine transitions ───────────────────────────────

    [Fact]
    public async Task Terminate_ActiveContract_SetsStatusTerminatedAndSetsTerminationDate()
    {
        await SeedBaseData();

        var terminationDate = new DateOnly(2026, 6, 30);
        var result = await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = _contract.Id, TerminationDate = terminationDate
        }, default);

        result.Status.Should().Be(ContractStatus.Terminated.ToString());
        result.TerminationDate.Should().Be(terminationDate);

        var dbContract = await _fixture.DbContext.Contracts.FindAsync(_contract.Id);
        dbContract!.Status.Should().Be(ContractStatus.Terminated);
        dbContract.TerminationDate.Should().Be(terminationDate);
    }

    [Fact]
    public async Task Terminate_OccupiedRoom_SetsRoomToAvailable()
    {
        await SeedBaseData(roomStatus: RoomStatus.Occupied);

        await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = _contract.Id, TerminationDate = new DateOnly(2026, 6, 30)
        }, default);

        var dbRoom = await _fixture.DbContext.Rooms.FindAsync(_room.Id);
        dbRoom!.Status.Should().Be(RoomStatus.Available);
    }

    [Fact]
    public async Task Terminate_MaintenanceRoom_DoesNotChangeRoomStatus()
    {
        await SeedBaseData(roomStatus: RoomStatus.Maintenance);

        await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = _contract.Id, TerminationDate = new DateOnly(2026, 6, 30)
        }, default);

        var dbRoom = await _fixture.DbContext.Rooms.FindAsync(_room.Id);
        dbRoom!.Status.Should().Be(RoomStatus.Maintenance);
    }

    // ── SD-02: ContractTenant MoveOutDate ──────────────────────────────────────

    [Fact]
    public async Task Terminate_WithActiveTenants_SetsMoveOutDateOnAllOpenTenants()
    {
        await SeedBaseData();
        await AddContractTenant();
        await AddContractTenant();

        var terminationDate = new DateOnly(2026, 6, 30);
        await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = _contract.Id, TerminationDate = terminationDate
        }, default);

        var tenants = await _fixture.DbContext.ContractTenants
            .Where(ct => ct.ContractId == _contract.Id)
            .ToListAsync();
        tenants.Should().AllSatisfy(ct => ct.MoveOutDate.Should().Be(terminationDate));
    }

    [Fact]
    public async Task Terminate_TenantAlreadyMovedOut_DoesNotOverrideMoveOutDate()
    {
        await SeedBaseData();
        // SeedBaseData already created an active main tenant (MoveOutDate == null).
        // Add a second, non-main tenant who has already moved out.
        var existingMoveOut = new DateOnly(2026, 3, 15);
        await AddContractTenant(moveOutDate: existingMoveOut);

        await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = _contract.Id, TerminationDate = new DateOnly(2026, 6, 30)
        }, default);

        // The already-moved-out tenant keeps their original MoveOutDate (not overridden) …
        var movedOutTenant = await _fixture.DbContext.ContractTenants
            .FirstAsync(ct => ct.ContractId == _contract.Id && !ct.IsMainTenant);
        movedOutTenant.MoveOutDate.Should().Be(existingMoveOut);

        // … while the still-active main tenant receives the termination date.
        var mainTenant = await _fixture.DbContext.ContractTenants
            .FirstAsync(ct => ct.ContractId == _contract.Id && ct.IsMainTenant);
        mainTenant.MoveOutDate.Should().Be(new DateOnly(2026, 6, 30));
    }

    // ── Auto-void future invoices ──────────────────────────────────────────────

    [Fact]
    public async Task Terminate_FutureUnpaidInvoices_AreAutoVoided()
    {
        await SeedBaseData();
        // Terminate June 2026 — July and August invoices should be voided
        await AddFutureInvoice(2026, 7, InvoiceStatus.Draft);
        await AddFutureInvoice(2026, 8, InvoiceStatus.Sent);

        await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = _contract.Id, TerminationDate = new DateOnly(2026, 6, 30)
        }, default);

        var futureInvoices = await _fixture.DbContext.Invoices
            .Where(i => i.ContractId == _contract.Id &&
                        (i.BillingYear > 2026 ||
                         (i.BillingYear == 2026 && i.BillingMonth > 6)))
            .ToListAsync();
        futureInvoices.Should().AllSatisfy(i => i.Status.Should().Be(InvoiceStatus.Void));
    }

    [Fact]
    public async Task Terminate_CurrentMonthInvoice_IsNotVoided()
    {
        await SeedBaseData();
        await AddFutureInvoice(2026, 6, InvoiceStatus.Draft);  // termination month — must NOT be voided

        await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = _contract.Id, TerminationDate = new DateOnly(2026, 6, 30)
        }, default);

        var juneInvoice = await _fixture.DbContext.Invoices
            .FirstAsync(i => i.ContractId == _contract.Id && i.BillingMonth == 6 && i.BillingYear == 2026);
        juneInvoice.Status.Should().Be(InvoiceStatus.Draft,
            "the invoice for the termination month itself should not be auto-voided");
    }

    [Fact]
    public async Task Terminate_FutureInvoiceWithPayment_IsNotVoided()
    {
        await SeedBaseData();
        await AddFutureInvoice(2026, 7, InvoiceStatus.Sent, withPayment: true);

        await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = _contract.Id, TerminationDate = new DateOnly(2026, 6, 30)
        }, default);

        var invoice = await _fixture.DbContext.Invoices
            .FirstAsync(i => i.ContractId == _contract.Id && i.BillingMonth == 7);
        invoice.Status.Should().Be(InvoiceStatus.Sent,
            "an invoice that has a payment should not be auto-voided");
    }

    // ── Notification created ───────────────────────────────────────────────────

    [Fact]
    public async Task Terminate_Success_CreatesNotificationForTenant()
    {
        await SeedBaseData();

        await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = _contract.Id, TerminationDate = new DateOnly(2026, 6, 30)
        }, default);

        var notification = await _fixture.DbContext.Notifications
            .FirstOrDefaultAsync(n => n.UserId == _tenant.Id);
        notification.Should().NotBeNull("tenant must be notified about contract termination");
    }

    // ── Validation errors ──────────────────────────────────────────────────────

    [Fact]
    public async Task Terminate_AlreadyTerminated_ThrowsConflictException()
    {
        await SeedBaseData();
        // Manually terminate first
        _contract.Status = ContractStatus.Terminated;
        _fixture.DbContext.Contracts.Update(_contract);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        var act = () => CreateHandler().Handle(new TerminateContractCommand
        {
            Id = _contract.Id, TerminationDate = new DateOnly(2026, 7, 31)
        }, default);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Terminate_NegativeDeductions_ThrowsBadRequestException()
    {
        await SeedBaseData();

        var act = () => CreateHandler().Handle(new TerminateContractCommand
        {
            Id = _contract.Id,
            TerminationDate = new DateOnly(2026, 6, 30),
            Deductions = -1_000_000
        }, default);

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task Terminate_DeductionsExceedDeposit_ThrowsBadRequestException()
    {
        await SeedBaseData(deposit: 10_000_000);

        var act = () => CreateHandler().Handle(new TerminateContractCommand
        {
            Id = _contract.Id,
            TerminationDate = new DateOnly(2026, 6, 30),
            Deductions = 12_000_000   // exceeds 10M
        }, default);

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task Terminate_TerminationDateBeforeStartDate_ThrowsBadRequestException()
    {
        await SeedBaseData(); // StartDate = 2026-01-01

        var act = () => CreateHandler().Handle(new TerminateContractCommand
        {
            Id = _contract.Id,
            TerminationDate = new DateOnly(2025, 12, 31)
        }, default);

        await act.Should().ThrowAsync<BadRequestException>();
    }

    // ── Note persisted ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Terminate_WithNote_PersistsTerminationNoteToDb()
    {
        await SeedBaseData();

        await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = _contract.Id,
            TerminationDate = new DateOnly(2026, 6, 30),
            Note = "Hư hỏng tường và cửa sổ"
        }, default);

        var dbContract = await _fixture.DbContext.Contracts.FindAsync(_contract.Id);
        dbContract!.TerminationNote.Should().Be("Hư hỏng tường và cửa sổ");

        var refundPayment = await _fixture.DbContext.Payments
            .FirstAsync(p => p.ContractId == _contract.Id && p.Type == PaymentType.DepositRefund);
        refundPayment.Note.Should().Be("Hư hỏng tường và cửa sổ");
    }
}
