using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Payments.Commands;
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


public class RecordPaymentIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();

    private User     _owner    = null!;
    private User     _tenant   = null!;
    private Building _building = null!;
    private Room     _room     = null!;
    private Contract _contract = null!;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync()    => await _fixture.DisposeAsync();

    private async Task SetupBaseData()
    {
        _owner    = TestDataBuilder.CreateUser(role: UserRole.Owner);
        _tenant   = TestDataBuilder.CreateUser(role: UserRole.Tenant);
        _building = TestDataBuilder.CreateBuilding(_owner.Id);
        _room     = TestDataBuilder.CreateRoom(_building.Id);
        _contract = TestDataBuilder.CreateContract(_room.Id, _tenant.Id, _owner.Id);

        _fixture.DbContext.Users.AddRange(_owner, _tenant);
        _fixture.DbContext.Buildings.Add(_building);
        _fixture.DbContext.Rooms.Add(_room);
        _fixture.DbContext.Contracts.Add(_contract);
        await _fixture.DbContext.SaveChangesAsync();
    }

    private RecordPaymentCommandHandler CreateHandler()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(m => m.GetRequiredUserId()).Returns(_owner.Id);
        currentUser.Setup(m => m.IsOwner).Returns(true);
        currentUser.Setup(m => m.Role).Returns(UserRole.Owner);

        var buildingScope = new BuildingScopeService(_fixture.DbContext, currentUser.Object);

        var emailService = new Mock<IEmailService>();
        emailService
            .Setup(m => m.TrySendAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        return new RecordPaymentCommandHandler(
            _fixture.DbContext, currentUser.Object, buildingScope, emailService.Object);
    }

    // ── PAY-03: Sent → PartiallyPaid ─────────────────────────────────────────

    [Fact]
    public async Task RecordPayment_PartialAmount_TransitionsToPartiallyPaid()
    {
        await SetupBaseData();
        var invoice = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id,
            rentAmount: 5_000_000, status: InvoiceStatus.Sent);
        _fixture.DbContext.Invoices.Add(invoice);
        await _fixture.DbContext.SaveChangesAsync();

        await CreateHandler().Handle(
            new RecordPaymentCommand { InvoiceId = invoice.Id, Amount = 2_000_000 }, default);

        var persisted = await _fixture.DbContext.Invoices.FindAsync(invoice.Id);
        persisted!.Status.Should().Be(InvoiceStatus.PartiallyPaid);

        var payments = await _fixture.DbContext.Payments
            .Where(p => p.InvoiceId == invoice.Id)
            .ToListAsync();
        payments.Should().HaveCount(1);
        payments[0].Amount.Should().Be(2_000_000);
        payments[0].Type.Should().Be(PaymentType.RentPayment);
    }

    // ── PAY-03: PartiallyPaid → Paid ─────────────────────────────────────────

    [Fact]
    public async Task RecordPayment_RemainingAmount_TransitionsFromPartiallyPaidToPaid()
    {
        await SetupBaseData();
        var invoice = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id,
            rentAmount: 5_000_000, status: InvoiceStatus.PartiallyPaid);
        var existingPayment = TestDataBuilder.CreatePayment(invoice.Id, _owner.Id, amount: 2_000_000);
        _fixture.DbContext.Invoices.Add(invoice);
        _fixture.DbContext.Payments.Add(existingPayment);
        await _fixture.DbContext.SaveChangesAsync();

        await CreateHandler().Handle(
            new RecordPaymentCommand { InvoiceId = invoice.Id, Amount = 3_000_000 }, default);

        var persisted = await _fixture.DbContext.Invoices.FindAsync(invoice.Id);
        persisted!.Status.Should().Be(InvoiceStatus.Paid);
    }

    // ── PAY-03: Sent → Paid (một lần thanh toán đúng bằng tổng) ─────────────

    [Fact]
    public async Task RecordPayment_ExactTotalAmount_TransitionsFromSentToPaid()
    {
        await SetupBaseData();
        var invoice = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id,
            rentAmount: 5_000_000, status: InvoiceStatus.Sent);
        _fixture.DbContext.Invoices.Add(invoice);
        await _fixture.DbContext.SaveChangesAsync();

        await CreateHandler().Handle(
            new RecordPaymentCommand { InvoiceId = invoice.Id, Amount = 5_000_000 }, default);

        var persisted = await _fixture.DbContext.Invoices.FindAsync(invoice.Id);
        persisted!.Status.Should().Be(InvoiceStatus.Paid);
    }

    // ── PAY-03: Overdue → PartiallyPaid / Paid ────────────────────────────────

    [Theory]
    [InlineData(1_000_000, InvoiceStatus.PartiallyPaid)]
    [InlineData(5_000_000, InvoiceStatus.Paid)]
    public async Task RecordPayment_OverdueInvoice_TransitionsCorrectly(
        decimal amount, InvoiceStatus expectedStatus)
    {
        await SetupBaseData();
        var invoice = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id,
            rentAmount: 5_000_000, status: InvoiceStatus.Overdue);
        _fixture.DbContext.Invoices.Add(invoice);
        await _fixture.DbContext.SaveChangesAsync();

        await CreateHandler().Handle(
            new RecordPaymentCommand { InvoiceId = invoice.Id, Amount = amount }, default);

        var persisted = await _fixture.DbContext.Invoices.FindAsync(invoice.Id);
        persisted!.Status.Should().Be(expectedStatus);
    }

    // ── PAY-01: PaidAmount tính động từ sum(Payments) — không lưu trên Invoice ─

    [Fact]
    public async Task RecordPayment_MultiplePayments_PaidAmountIsSumOfPayments_NotStoredOnInvoice()
    {
        await SetupBaseData();
        var invoice = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id,
            rentAmount: 5_000_000, status: InvoiceStatus.Sent);
        _fixture.DbContext.Invoices.Add(invoice);
        await _fixture.DbContext.SaveChangesAsync();

        var handler = CreateHandler();
        await handler.Handle(new RecordPaymentCommand { InvoiceId = invoice.Id, Amount = 1_000_000 }, default);
        await handler.Handle(new RecordPaymentCommand { InvoiceId = invoice.Id, Amount = 1_500_000 }, default);

        // Invoice entity has no PaidAmount field — computed from Payments
        var payments = await _fixture.DbContext.Payments
            .Where(p => p.InvoiceId == invoice.Id && p.Type == PaymentType.RentPayment)
            .ToListAsync();
        payments.Should().HaveCount(2);
        payments.Sum(p => p.Amount).Should().Be(2_500_000);

        // Status reflects cumulative total
        var persisted = await _fixture.DbContext.Invoices.FindAsync(invoice.Id);
        persisted!.Status.Should().Be(InvoiceStatus.PartiallyPaid);
    }

    // ── Overpayment guard ─────────────────────────────────────────────────────

    [Fact]
    public async Task RecordPayment_AmountExceedsTotal_ThrowsBadRequestException()
    {
        await SetupBaseData();
        var invoice = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id,
            rentAmount: 5_000_000, status: InvoiceStatus.Sent);
        _fixture.DbContext.Invoices.Add(invoice);
        await _fixture.DbContext.SaveChangesAsync();

        var act = () => CreateHandler().Handle(
            new RecordPaymentCommand { InvoiceId = invoice.Id, Amount = 6_000_000 }, default);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*vượt quá số dư*");
    }

    [Fact]
    public async Task RecordPayment_AmountExceedsRemaining_AfterPartialPayment_ThrowsBadRequestException()
    {
        // Đã trả 4M, còn lại 1M, nhưng cố trả thêm 2M → rejected
        await SetupBaseData();
        var invoice = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id,
            rentAmount: 5_000_000, status: InvoiceStatus.PartiallyPaid);
        var existingPayment = TestDataBuilder.CreatePayment(invoice.Id, _owner.Id, amount: 4_000_000);
        _fixture.DbContext.Invoices.Add(invoice);
        _fixture.DbContext.Payments.Add(existingPayment);
        await _fixture.DbContext.SaveChangesAsync();

        var act = () => CreateHandler().Handle(
            new RecordPaymentCommand { InvoiceId = invoice.Id, Amount = 2_000_000 }, default);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*vượt quá số dư*");
    }

    // ── PAY-04: Blocked statuses ──────────────────────────────────────────────

    [Fact]
    public async Task RecordPayment_DraftInvoice_ThrowsConflictException()
    {
        await SetupBaseData();
        var invoice = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id,
            status: InvoiceStatus.Draft);
        _fixture.DbContext.Invoices.Add(invoice);
        await _fixture.DbContext.SaveChangesAsync();

        var act = () => CreateHandler().Handle(
            new RecordPaymentCommand { InvoiceId = invoice.Id, Amount = 1_000_000 }, default);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Nháp*");
    }

    [Fact]
    public async Task RecordPayment_VoidInvoice_ThrowsConflictException()
    {
        await SetupBaseData();
        var invoice = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id,
            status: InvoiceStatus.Void);
        _fixture.DbContext.Invoices.Add(invoice);
        await _fixture.DbContext.SaveChangesAsync();

        var act = () => CreateHandler().Handle(
            new RecordPaymentCommand { InvoiceId = invoice.Id, Amount = 1_000_000 }, default);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*hủy*");
    }

    [Fact]
    public async Task RecordPayment_AlreadyPaidInvoice_ThrowsConflictException()
    {
        await SetupBaseData();
        var invoice = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id,
            status: InvoiceStatus.Paid);
        _fixture.DbContext.Invoices.Add(invoice);
        await _fixture.DbContext.SaveChangesAsync();

        var act = () => CreateHandler().Handle(
            new RecordPaymentCommand { InvoiceId = invoice.Id, Amount = 1_000_000 }, default);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*đầy đủ*");
    }

    // ── Payment persisted với đúng metadata ──────────────────────────────────

    [Fact]
    public async Task RecordPayment_CreatesPaymentRecord_WithCorrectFields()
    {
        await SetupBaseData();
        var invoice = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id,
            rentAmount: 5_000_000, status: InvoiceStatus.Sent);
        _fixture.DbContext.Invoices.Add(invoice);
        await _fixture.DbContext.SaveChangesAsync();

        var result = await CreateHandler().Handle(
            new RecordPaymentCommand
            {
                InvoiceId = invoice.Id,
                Amount = 1_000_000,
                PaymentMethod = "BankTransfer",
                Note = "Thanh toán tháng 3"
            }, default);

        result.Amount.Should().Be(1_000_000);
        result.Type.Should().Be(PaymentType.RentPayment.ToString());
        result.RecordedBy.Should().Be(_owner.Id);

        var payment = await _fixture.DbContext.Payments
            .FirstAsync(p => p.InvoiceId == invoice.Id);
        payment.PaymentMethod.Should().Be("BankTransfer");
        payment.Note.Should().Be("Thanh toán tháng 3");
        payment.RecordedBy.Should().Be(_owner.Id);
    }

    // ── Notification được tạo sau khi thanh toán ─────────────────────────────

    [Fact]
    public async Task RecordPayment_CreatesNotificationForTenant()
    {
        await SetupBaseData();
        var invoice = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id,
            rentAmount: 5_000_000, status: InvoiceStatus.Sent);
        _fixture.DbContext.Invoices.Add(invoice);
        await _fixture.DbContext.SaveChangesAsync();

        await CreateHandler().Handle(
            new RecordPaymentCommand { InvoiceId = invoice.Id, Amount = 1_000_000 }, default);

        var notification = await _fixture.DbContext.Notifications
            .FirstOrDefaultAsync(n => n.UserId == _tenant.Id
                && n.ReferenceId == invoice.Id);
        notification.Should().NotBeNull();
    }
}
