using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Contracts.Commands;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using MockQueryable.Moq;
using Moq;
using Xunit;

namespace ElysStay.Tests.Unit.Business;

/// <summary>
/// Unit tests for TerminateContractCommandHandler.
/// Verifies move-out business rules: deposit calculation, state machine transitions,
/// auto-void of future invoices, audit payment creation.
/// </summary>
public class TerminateContractUnitTests
{
    private readonly Mock<IApplicationDbContext> _db          = new();
    private readonly Mock<ICurrentUserService>   _currentUser = new();
    private readonly Mock<IBuildingScopeService> _buildingScope = new();
    private readonly Mock<IEmailService>         _emailService  = new();

    private TerminateContractCommandHandler CreateHandler()
        => new(_db.Object, _currentUser.Object, _buildingScope.Object, _emailService.Object);

    // ── Test data factory ──────────────────────────────────────────────────────

    private record Fixture(Building Building, Room Room, User Tenant, Contract Contract);

    private static Fixture CreateFixture(
        decimal deposit = 10_000_000,
        RoomStatus roomStatus = RoomStatus.Occupied,
        ContractStatus contractStatus = ContractStatus.Active)
    {
        var buildingId = Guid.NewGuid();
        var building   = new Building { Id = buildingId, Name = "Tòa A", InvoiceDueDay = 10 };
        var room       = new Room
        {
            Id = Guid.NewGuid(), BuildingId = buildingId, Building = building,
            RoomNumber = "101", Status = roomStatus
        };
        var tenant   = new User { Id = Guid.NewGuid(), FullName = "Test Tenant", Email = "t@t.com" };
        var contract = new Contract
        {
            Id            = Guid.NewGuid(),
            RoomId        = room.Id,
            Room          = room,
            TenantUserId  = tenant.Id,
            TenantUser    = tenant,
            MonthlyRent   = 5_000_000,
            DepositAmount = deposit,
            DepositStatus = DepositStatus.Held,
            Status        = contractStatus,
            StartDate     = new DateOnly(2026, 1, 1),
            MoveInDate    = new DateOnly(2026, 1, 1),
            EndDate       = new DateOnly(2027, 12, 31)
        };
        room.Building = building;
        return new Fixture(building, room, tenant, contract);
    }

    private void SetupMocks(Fixture f, List<Invoice>? invoices = null)
    {
        _currentUser.Setup(m => m.GetRequiredUserId()).Returns(Guid.NewGuid());
        _buildingScope
            .Setup(m => m.AuthorizeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _emailService
            .Setup(m => m.TrySendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _db.Setup(m => m.Contracts)
            .Returns(new List<Contract> { f.Contract }.AsQueryable().BuildMockDbSet().Object);
        _db.Setup(m => m.Invoices)
            .Returns((invoices ?? []).AsQueryable().BuildMockDbSet().Object);

        // Capture added payments
        var paymentsMock = new Mock<Microsoft.EntityFrameworkCore.DbSet<Payment>>();
        _db.Setup(m => m.Payments).Returns(paymentsMock.Object);

        var notificationsMock = new Mock<Microsoft.EntityFrameworkCore.DbSet<Notification>>();
        _db.Setup(m => m.Notifications).Returns(notificationsMock.Object);

        _db.Setup(m => m.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    // ── DEP-04: Deposit calculation ────────────────────────────────────────────

    [Fact]
    public async Task Handle_NoDeductions_FullRefund_SetsRefundedStatus()
    {
        // Deposit=10M, Deductions=0 → RefundAmount=10M, DepositStatus=Refunded
        var f = CreateFixture(deposit: 10_000_000);
        SetupMocks(f);

        var result = await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = f.Contract.Id,
            TerminationDate = new DateOnly(2026, 6, 30),
            Deductions = 0
        }, default);

        result.RefundAmount.Should().Be(10_000_000);
        result.DepositStatus.Should().Be(DepositStatus.Refunded.ToString());
        f.Contract.DepositStatus.Should().Be(DepositStatus.Refunded);
        f.Contract.RefundAmount.Should().Be(10_000_000);
    }

    [Fact]
    public async Task Handle_PartialDeductions_SetsPartiallyRefundedStatus()
    {
        // Deposit=10M, Deductions=2M → RefundAmount=8M, DepositStatus=PartiallyRefunded
        var f = CreateFixture(deposit: 10_000_000);
        SetupMocks(f);

        var result = await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = f.Contract.Id,
            TerminationDate = new DateOnly(2026, 6, 30),
            Deductions = 2_000_000
        }, default);

        result.RefundAmount.Should().Be(8_000_000);
        result.DepositStatus.Should().Be(DepositStatus.PartiallyRefunded.ToString());
        f.Contract.DepositStatus.Should().Be(DepositStatus.PartiallyRefunded);
        f.Contract.RefundAmount.Should().Be(8_000_000);
    }

    [Fact]
    public async Task Handle_FullDeductions_SetsForfeitedStatus()
    {
        // Deposit=10M, Deductions=10M → RefundAmount=0, DepositStatus=Forfeited
        var f = CreateFixture(deposit: 10_000_000);
        SetupMocks(f);

        var result = await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = f.Contract.Id,
            TerminationDate = new DateOnly(2026, 6, 30),
            Deductions = 10_000_000
        }, default);

        result.RefundAmount.Should().Be(0);
        result.DepositStatus.Should().Be(DepositStatus.Forfeited.ToString());
        f.Contract.DepositStatus.Should().Be(DepositStatus.Forfeited);
        f.Contract.RefundAmount.Should().Be(0);
    }

    // ── SM-10: Contract state machine ──────────────────────────────────────────

    [Fact]
    public async Task Handle_ActiveContract_SetsTerminatedStatus()
    {
        var f = CreateFixture();
        SetupMocks(f);

        await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = f.Contract.Id,
            TerminationDate = new DateOnly(2026, 6, 30)
        }, default);

        f.Contract.Status.Should().Be(ContractStatus.Terminated);
        f.Contract.TerminationDate.Should().Be(new DateOnly(2026, 6, 30));
    }

    [Fact]
    public async Task Handle_AlreadyTerminated_ThrowsConflictException()
    {
        var f = CreateFixture(contractStatus: ContractStatus.Terminated);
        SetupMocks(f);

        var act = () => CreateHandler().Handle(new TerminateContractCommand
        {
            Id = f.Contract.Id, TerminationDate = new DateOnly(2026, 6, 30)
        }, default);

        await act.Should().ThrowAsync<ConflictException>();
    }

    // ── SM-04: Room status transition ──────────────────────────────────────────

    [Fact]
    public async Task Handle_OccupiedRoom_SetsRoomToAvailable()
    {
        var f = CreateFixture(roomStatus: RoomStatus.Occupied);
        SetupMocks(f);

        await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = f.Contract.Id, TerminationDate = new DateOnly(2026, 6, 30)
        }, default);

        f.Room.Status.Should().Be(RoomStatus.Available);
    }

    [Fact]
    public async Task Handle_MaintenanceRoom_DoesNotChangeRoomStatus()
    {
        // If room is in Maintenance, contract termination should NOT override it to Available
        var f = CreateFixture(roomStatus: RoomStatus.Maintenance);
        SetupMocks(f);

        await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = f.Contract.Id, TerminationDate = new DateOnly(2026, 6, 30)
        }, default);

        f.Room.Status.Should().Be(RoomStatus.Maintenance);
    }

    // ── SD-02: ContractTenant MoveOutDate ──────────────────────────────────────

    [Fact]
    public async Task Handle_ActiveTenants_SetsMoveOutDateOnAllTenants()
    {
        var f = CreateFixture();
        f.Contract.ContractTenants.Add(new ContractTenant
        {
            Id = Guid.NewGuid(), ContractId = f.Contract.Id,
            TenantUserId = Guid.NewGuid(), MoveInDate = new DateOnly(2026, 1, 1)
        });
        f.Contract.ContractTenants.Add(new ContractTenant
        {
            Id = Guid.NewGuid(), ContractId = f.Contract.Id,
            TenantUserId = Guid.NewGuid(), MoveInDate = new DateOnly(2026, 2, 1)
        });
        SetupMocks(f);

        var terminationDate = new DateOnly(2026, 6, 30);
        await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = f.Contract.Id, TerminationDate = terminationDate
        }, default);

        f.Contract.ContractTenants.Should().AllSatisfy(ct =>
            ct.MoveOutDate.Should().Be(terminationDate));
    }

    [Fact]
    public async Task Handle_TenantAlreadyMovedOut_DoesNotOverrideMoveOutDate()
    {
        var existing = new DateOnly(2026, 3, 15);
        var f = CreateFixture();
        f.Contract.ContractTenants.Add(new ContractTenant
        {
            Id = Guid.NewGuid(), ContractId = f.Contract.Id,
            TenantUserId = Guid.NewGuid(), MoveInDate = new DateOnly(2026, 1, 1),
            MoveOutDate = existing    // already moved out
        });
        SetupMocks(f);

        await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = f.Contract.Id, TerminationDate = new DateOnly(2026, 6, 30)
        }, default);

        f.Contract.ContractTenants.Single().MoveOutDate.Should().Be(existing);
    }

    // ── Validation guards ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ContractNotFound_ThrowsNotFoundException()
    {
        _currentUser.Setup(m => m.GetRequiredUserId()).Returns(Guid.NewGuid());
        _db.Setup(m => m.Contracts)
            .Returns(new List<Contract>().AsQueryable().BuildMockDbSet().Object);

        var act = () => CreateHandler().Handle(new TerminateContractCommand
        {
            Id = Guid.NewGuid(), TerminationDate = new DateOnly(2026, 6, 30)
        }, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_TerminationDateBeforeStartDate_ThrowsBadRequestException()
    {
        var f = CreateFixture(); // StartDate = 2026-01-01
        SetupMocks(f);

        var act = () => CreateHandler().Handle(new TerminateContractCommand
        {
            Id = f.Contract.Id,
            TerminationDate = new DateOnly(2025, 12, 31)  // before StartDate
        }, default);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*trước ngày bắt đầu*");
    }

    [Fact]
    public async Task Handle_TerminationDateBeforeMoveInDate_ThrowsBadRequestException()
    {
        var f = CreateFixture(); // MoveInDate = 2026-01-01
        SetupMocks(f);

        // Set start before move-in to make only move-in validation fail
        f.Contract.StartDate = new DateOnly(2025, 12, 1);
        f.Contract.MoveInDate = new DateOnly(2026, 1, 15);

        var act = () => CreateHandler().Handle(new TerminateContractCommand
        {
            Id = f.Contract.Id,
            TerminationDate = new DateOnly(2026, 1, 10)  // after StartDate but before MoveInDate
        }, default);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*trước ngày dọn vào*");
    }

    [Fact]
    public async Task Handle_NegativeDeductions_ThrowsBadRequestException()
    {
        var f = CreateFixture();
        SetupMocks(f);

        var act = () => CreateHandler().Handle(new TerminateContractCommand
        {
            Id = f.Contract.Id,
            TerminationDate = new DateOnly(2026, 6, 30),
            Deductions = -500_000
        }, default);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*không được âm*");
    }

    [Fact]
    public async Task Handle_DeductionsExceedDeposit_ThrowsBadRequestException()
    {
        var f = CreateFixture(deposit: 10_000_000);
        SetupMocks(f);

        var act = () => CreateHandler().Handle(new TerminateContractCommand
        {
            Id = f.Contract.Id,
            TerminationDate = new DateOnly(2026, 6, 30),
            Deductions = 15_000_000   // exceeds 10M deposit
        }, default);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*không được vượt quá*");
    }

    // ── Auto-void future invoices ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_FutureUnpaidInvoices_VoidsThemOnTermination()
    {
        // Terminate June 2026 → invoices in July and August should be voided
        var f = CreateFixture();
        var futureInvoices = new List<Invoice>
        {
            new()
            {
                Id = Guid.NewGuid(), ContractId = f.Contract.Id,
                BillingYear = 2026, BillingMonth = 7, Status = InvoiceStatus.Draft
            },
            new()
            {
                Id = Guid.NewGuid(), ContractId = f.Contract.Id,
                BillingYear = 2026, BillingMonth = 8, Status = InvoiceStatus.Sent
            }
        };
        SetupMocks(f, invoices: futureInvoices);

        await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = f.Contract.Id, TerminationDate = new DateOnly(2026, 6, 30)
        }, default);

        futureInvoices.Should().AllSatisfy(i => i.Status.Should().Be(InvoiceStatus.Void));
    }

    [Fact]
    public async Task Handle_CurrentMonthInvoice_IsNotVoided()
    {
        // Terminate June 2026 — June invoice should NOT be voided
        var f = CreateFixture();
        var currentMonthInvoice = new Invoice
        {
            Id = Guid.NewGuid(), ContractId = f.Contract.Id,
            BillingYear = 2026, BillingMonth = 6, Status = InvoiceStatus.Draft
        };
        SetupMocks(f, invoices: [currentMonthInvoice]);

        await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = f.Contract.Id, TerminationDate = new DateOnly(2026, 6, 30)
        }, default);

        currentMonthInvoice.Status.Should().Be(InvoiceStatus.Draft);
    }

    [Fact]
    public async Task Handle_FutureInvoiceWithPayments_IsNotVoided()
    {
        // Future invoice that already has a payment should be left as-is
        var f = CreateFixture();
        var paidInvoice = new Invoice
        {
            Id = Guid.NewGuid(), ContractId = f.Contract.Id,
            BillingYear = 2026, BillingMonth = 7, Status = InvoiceStatus.Sent
        };
        paidInvoice.Payments.Add(new Payment
        {
            InvoiceId = paidInvoice.Id, Type = PaymentType.RentPayment, Amount = 1_000_000
        });
        SetupMocks(f, invoices: [paidInvoice]);

        await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = f.Contract.Id, TerminationDate = new DateOnly(2026, 6, 30)
        }, default);

        paidInvoice.Status.Should().Be(InvoiceStatus.Sent, "invoice with payments must not be auto-voided");
    }

    [Fact]
    public async Task Handle_AlreadyVoidedFutureInvoice_RemainsVoided()
    {
        var f = CreateFixture();
        var voidedInvoice = new Invoice
        {
            Id = Guid.NewGuid(), ContractId = f.Contract.Id,
            BillingYear = 2026, BillingMonth = 7, Status = InvoiceStatus.Void
        };
        SetupMocks(f, invoices: [voidedInvoice]);

        await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = f.Contract.Id, TerminationDate = new DateOnly(2026, 6, 30)
        }, default);

        voidedInvoice.Status.Should().Be(InvoiceStatus.Void);
    }

    // ── TerminationNote persisted ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithNote_PersistsTerminationNote()
    {
        var f = CreateFixture();
        SetupMocks(f);

        await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = f.Contract.Id,
            TerminationDate = new DateOnly(2026, 6, 30),
            Note = "Hư hỏng tường và cửa"
        }, default);

        f.Contract.TerminationNote.Should().Be("Hư hỏng tường và cửa");
    }

    // ── SaveChanges called ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Success_CallsSaveChanges()
    {
        var f = CreateFixture();
        SetupMocks(f);

        await CreateHandler().Handle(new TerminateContractCommand
        {
            Id = f.Contract.Id, TerminationDate = new DateOnly(2026, 6, 30)
        }, default);

        _db.Verify(m => m.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
