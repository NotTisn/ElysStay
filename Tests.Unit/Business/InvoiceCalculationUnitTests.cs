using Application.Common.Exceptions;
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
/// Unit tests for UpdateInvoiceCommandHandler business rules.
/// Mocks IApplicationDbContext, ICurrentUserService, IBuildingScopeService
/// so only the handler's own logic is under test.
/// </summary>
public class InvoiceCalculationUnitTests
{
    private readonly Mock<IApplicationDbContext> _db = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IBuildingScopeService> _buildingScope = new();

    private UpdateInvoiceCommandHandler CreateHandler()
        => new(_db.Object, _currentUser.Object, _buildingScope.Object);

    private static Invoice BuildInvoice(
        InvoiceStatus status = InvoiceStatus.Draft,
        decimal rent = 5_000_000,
        decimal service = 500_000,
        decimal penalty = 0,
        decimal discount = 0,
        List<Payment>? payments = null)
    {
        var building = new Building { Id = Guid.NewGuid(), Name = "Tòa A" };
        var room = new Room { Id = Guid.NewGuid(), RoomNumber = "101", BuildingId = building.Id, Building = building };
        var tenant = new User { Id = Guid.NewGuid(), FullName = "Nguyễn Văn A", Email = "tenant@test.com" };
        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            Room = room,
            TenantUserId = tenant.Id,
            TenantUser = tenant
        };

        return new Invoice
        {
            Id = Guid.NewGuid(),
            ContractId = contract.Id,
            Contract = contract,
            Status = status,
            BillingYear = 2025,
            BillingMonth = 5,
            RentAmount = rent,
            ServiceAmount = service,
            PenaltyAmount = penalty,
            DiscountAmount = discount,
            TotalAmount = rent + service + penalty - discount,
            Payments = payments ?? []
        };
    }

    private void SetupMocks(Invoice invoice)
    {
        _currentUser.Setup(m => m.GetRequiredUserId()).Returns(Guid.NewGuid());
        _buildingScope
            .Setup(m => m.AuthorizeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockSet = new List<Invoice> { invoice }.AsQueryable().BuildMockDbSet();
        _db.Setup(m => m.Invoices).Returns(mockSet.Object);
        _db.Setup(m => m.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    // ── Not Found ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenInvoiceNotFound_ThrowsNotFoundException()
    {
        _currentUser.Setup(m => m.GetRequiredUserId()).Returns(Guid.NewGuid());
        var emptySet = new List<Invoice>().AsQueryable().BuildMockDbSet();
        _db.Setup(m => m.Invoices).Returns(emptySet.Object);

        var act = () => CreateHandler().Handle(
            new UpdateInvoiceCommand { Id = Guid.NewGuid() }, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Status Guards ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.Void)]
    [InlineData(InvoiceStatus.Overdue)]
    [InlineData(InvoiceStatus.PartiallyPaid)]
    [InlineData(InvoiceStatus.Unpaid)]
    public async Task Handle_WhenStatusIsNotDraftOrSent_ThrowsConflictException(InvoiceStatus status)
    {
        var invoice = BuildInvoice(status: status);
        SetupMocks(invoice);

        var act = () => CreateHandler().Handle(
            new UpdateInvoiceCommand { Id = invoice.Id, PenaltyAmount = 100_000 }, default);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Theory]
    [InlineData(InvoiceStatus.Draft)]
    [InlineData(InvoiceStatus.Sent)]
    public async Task Handle_WhenStatusIsDraftOrSent_DoesNotThrowConflict(InvoiceStatus status)
    {
        var invoice = BuildInvoice(status: status);
        SetupMocks(invoice);

        var act = () => CreateHandler().Handle(
            new UpdateInvoiceCommand { Id = invoice.Id, PenaltyAmount = 100_000 }, default);

        await act.Should().NotThrowAsync<ConflictException>();
    }

    // ── Negative Total Guard ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenDiscountExceedsTotal_ThrowsBadRequestException()
    {
        // rent=1M, discount=2M → total = -1M
        var invoice = BuildInvoice(rent: 1_000_000, service: 0);
        SetupMocks(invoice);

        var act = () => CreateHandler().Handle(
            new UpdateInvoiceCommand { Id = invoice.Id, DiscountAmount = 2_000_000 }, default);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*không thể âm*");
    }

    // ── Paid Amount Guard ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenNewTotalLessThanPaidAmount_ThrowsBadRequestException()
    {
        // total = 5.5M, paid = 3M, discount = 4M → newTotal = 1.5M < 3M
        var payments = new List<Payment> { new() { Amount = 3_000_000, Type = PaymentType.RentPayment } };
        var invoice = BuildInvoice(rent: 5_000_000, service: 500_000, payments: payments);
        SetupMocks(invoice);

        var act = () => CreateHandler().Handle(
            new UpdateInvoiceCommand { Id = invoice.Id, DiscountAmount = 4_000_000 }, default);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*đã thanh toán*");
    }

    // ── TotalAmount Recalculation ─────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithPenalty_RecalculatesTotalCorrectly()
    {
        var invoice = BuildInvoice(rent: 5_000_000, service: 500_000);
        SetupMocks(invoice);

        var result = await CreateHandler().Handle(
            new UpdateInvoiceCommand { Id = invoice.Id, PenaltyAmount = 275_000 }, default);

        // 5_000_000 + 500_000 + 275_000 - 0 = 5_775_000
        result.TotalAmount.Should().Be(5_775_000);
        result.PenaltyAmount.Should().Be(275_000);
    }

    [Fact]
    public async Task Handle_WithDiscount_RecalculatesTotalCorrectly()
    {
        var invoice = BuildInvoice(rent: 5_000_000, service: 500_000);
        SetupMocks(invoice);

        var result = await CreateHandler().Handle(
            new UpdateInvoiceCommand { Id = invoice.Id, DiscountAmount = 500_000 }, default);

        // 5_000_000 + 500_000 + 0 - 500_000 = 5_000_000
        result.TotalAmount.Should().Be(5_000_000);
        result.DiscountAmount.Should().Be(500_000);
    }

    [Fact]
    public async Task Handle_WithPenaltyAndDiscount_RecalculatesTotalCorrectly()
    {
        var invoice = BuildInvoice(rent: 5_000_000, service: 500_000);
        SetupMocks(invoice);

        var result = await CreateHandler().Handle(
            new UpdateInvoiceCommand { Id = invoice.Id, PenaltyAmount = 200_000, DiscountAmount = 100_000 }, default);

        // 5_000_000 + 500_000 + 200_000 - 100_000 = 5_600_000
        result.TotalAmount.Should().Be(5_600_000);
    }

    // ── SaveChanges Behavior ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithNoChanges_DoesNotCallSaveChanges()
    {
        var invoice = BuildInvoice();
        SetupMocks(invoice);

        // Command has no PenaltyAmount, DiscountAmount, Note → changed = false
        await CreateHandler().Handle(new UpdateInvoiceCommand { Id = invoice.Id }, default);

        _db.Verify(m => m.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNoteChanged_SavesChangesOnce()
    {
        var invoice = BuildInvoice();
        SetupMocks(invoice);

        await CreateHandler().Handle(
            new UpdateInvoiceCommand { Id = invoice.Id, Note = "Khách yêu cầu gia hạn" }, default);

        _db.Verify(m => m.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPenaltyChanged_SavesChangesOnce()
    {
        var invoice = BuildInvoice();
        SetupMocks(invoice);

        await CreateHandler().Handle(
            new UpdateInvoiceCommand { Id = invoice.Id, PenaltyAmount = 50_000 }, default);

        _db.Verify(m => m.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── @Adjustment Exact Scenario ────────────────────────────────────────────

    [Fact]
    public async Task Handle_AdjustmentScenario_RentPlusPenaltyMinusDiscount_TotalIs5_400_000()
    {
        // Feature: Apply penalty and discount
        // RentAmount=5_000_000, PenaltyAmount=500_000, DiscountAmount=100_000
        // → TotalAmount = 5_000_000 + 500_000 - 100_000 = 5_400_000
        var invoice = BuildInvoice(rent: 5_000_000, service: 0);
        SetupMocks(invoice);

        var result = await CreateHandler().Handle(
            new UpdateInvoiceCommand { Id = invoice.Id, PenaltyAmount = 500_000, DiscountAmount = 100_000 }, default);

        result.RentAmount.Should().Be(5_000_000);
        result.PenaltyAmount.Should().Be(500_000);
        result.DiscountAmount.Should().Be(100_000);
        result.TotalAmount.Should().Be(5_400_000);
    }
}
