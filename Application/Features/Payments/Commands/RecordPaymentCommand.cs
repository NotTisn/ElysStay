using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Payments.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Payments.Commands;

/// <summary>
/// Records a payment on an invoice.
/// PAY-01: PaidAmount computed dynamically.
/// PAY-02: Amount always positive.
/// PAY-03: Auto-transition SENT/OVERDUE → PARTIALLY_PAID → PAID.
/// PAY-04: Cannot pay DRAFT or VOID.
/// </summary>
public record RecordPaymentCommand : IRequest<PaymentDto>
{
    public Guid InvoiceId { get; init; }
    public required decimal Amount { get; init; }
    public string? PaymentMethod { get; init; }
    public string? Note { get; init; }
}

public class RecordPaymentCommandHandler : IRequestHandler<RecordPaymentCommand, PaymentDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;

    public RecordPaymentCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }

    public async Task<PaymentDto> Handle(RecordPaymentCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        // Use serializable isolation to prevent concurrent overpayment
        await using var transaction = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        try
        {
            var invoice = await _db.Invoices
                .Include(i => i.Contract!).ThenInclude(c => c.Room!)
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken)
                ?? throw new NotFoundException("Invoice", request.InvoiceId);

            // PAY-04: Cannot pay DRAFT or VOID
            if (invoice.Status == InvoiceStatus.Draft)
                throw new ConflictException("Cannot record payment on a DRAFT invoice. Send it first.");
            if (invoice.Status == InvoiceStatus.Void)
                throw new ConflictException("Cannot record payment on a VOID invoice.");
            if (invoice.Status == InvoiceStatus.Paid)
                throw new ConflictException("Invoice is already fully paid.");

            await _buildingScope.AuthorizeAsync(invoice.Contract!.Room!.BuildingId, cancellationToken);

            // Overpayment guard: amount must not exceed remaining balance
            var currentPaid = invoice.Payments
                .Where(p => p.Type == PaymentType.RentPayment)
                .Sum(p => p.Amount);
            var remaining = invoice.TotalAmount - currentPaid;
            if (request.Amount > remaining)
                throw new BadRequestException($"Payment amount ({request.Amount}) exceeds remaining balance ({remaining}).");

            // Create payment
            var payment = new Payment
            {
                InvoiceId = invoice.Id,
                Type = PaymentType.RentPayment,
                Amount = request.Amount,
                PaymentMethod = request.PaymentMethod,
                Note = request.Note,
                PaidAt = DateTime.UtcNow,
                RecordedBy = userId
            };
            _db.Payments.Add(payment);

            // PAY-03: Auto-transition
            var totalPaid = currentPaid + request.Amount;

            if (totalPaid >= invoice.TotalAmount)
                invoice.Status = InvoiceStatus.Paid;
            else if (totalPaid > 0)
                invoice.Status = InvoiceStatus.PartiallyPaid;

            invoice.UpdatedAt = DateTime.UtcNow;

            // NT-05: Notify tenant that payment was recorded
            _db.Notifications.Add(new Domain.Entities.Notification
            {
                UserId = invoice.Contract!.TenantUserId,
                Title = "Thanh toán ghi nhận",
                Message = $"Thanh toán {request.Amount:N0}đ đã được ghi nhận cho hóa đơn tháng {invoice.BillingMonth}/{invoice.BillingYear}.",
                Type = "PAYMENT_RECORDED",
                ReferenceId = payment.Id,
            });

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new PaymentDto
            {
                Id = payment.Id,
                InvoiceId = payment.InvoiceId,
                ContractId = payment.ContractId,
                Type = payment.Type.ToString(),
                Amount = payment.Amount,
                PaymentMethod = payment.PaymentMethod,
                Note = payment.Note,
                PaidAt = payment.PaidAt,
                RecordedBy = payment.RecordedBy,
                CreatedAt = payment.CreatedAt
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
