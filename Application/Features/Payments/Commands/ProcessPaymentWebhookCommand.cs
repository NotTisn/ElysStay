using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Payments.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Payments.Commands;

/// <summary>
/// Process a bank-transfer payment notification using ReferenceCode idempotency.
/// Replaying the same payload must not create a second ledger effect.
/// </summary>
public record ProcessPaymentWebhookCommand : IRequest<PaymentDto>
{
    public Guid InvoiceId { get; init; }
    public required decimal Amount { get; init; }
    public required string ReferenceCode { get; init; }
    public string? Note { get; init; }
}

public class ProcessPaymentWebhookCommandHandler : IRequestHandler<ProcessPaymentWebhookCommand, PaymentDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;
    private readonly IEmailService _emailService;

    public ProcessPaymentWebhookCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope,
        IEmailService emailService)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
        _emailService = emailService;
    }

    public async Task<PaymentDto> Handle(ProcessPaymentWebhookCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();
        var normalizedReferenceCode = request.ReferenceCode.Trim();

        await using var transaction = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        try
        {
            var existingByReference = await _db.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.ReferenceCode == normalizedReferenceCode && p.PaymentMethod == "BankTransfer",
                    cancellationToken);

            if (existingByReference is not null)
            {
                if (existingByReference.InvoiceId != request.InvoiceId
                    || existingByReference.Amount != request.Amount
                    || existingByReference.Type != PaymentType.RentPayment)
                {
                    throw new ConflictException(
                        $"Mã giao dịch '{normalizedReferenceCode}' đã được dùng cho thanh toán khác.");
                }

                return new PaymentDto
                {
                    Id = existingByReference.Id,
                    InvoiceId = existingByReference.InvoiceId,
                    ContractId = existingByReference.ContractId,
                    ReservationId = existingByReference.ReservationId,
                    Type = existingByReference.Type.ToString(),
                    Amount = existingByReference.Amount,
                    PaymentMethod = existingByReference.PaymentMethod,
                    ReferenceCode = existingByReference.ReferenceCode,
                    Note = existingByReference.Note,
                    PaidAt = existingByReference.PaidAt,
                    RecordedBy = existingByReference.RecordedBy,
                    CreatedAt = existingByReference.CreatedAt
                };
            }

            var invoice = await _db.Invoices
                .Include(i => i.Contract!).ThenInclude(c => c.Room!).ThenInclude(r => r.Building!)
                .Include(i => i.Contract!).ThenInclude(c => c.ContractTenants).ThenInclude(ct => ct.Tenant!)
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken)
                ?? throw new NotFoundException("Hóa đơn", request.InvoiceId);

            await _buildingScope.AuthorizeAsync(invoice.Contract!.Room!.BuildingId, cancellationToken);

            if (invoice.Status == InvoiceStatus.Draft)
                throw new ConflictException("Không thể ghi nhận webhook cho hóa đơn Nháp. Hãy gửi hóa đơn trước.");
            if (invoice.Status == InvoiceStatus.Void)
                throw new ConflictException("Không thể ghi nhận webhook cho hóa đơn đã hủy.");
            if (invoice.Status == InvoiceStatus.Paid)
                throw new ConflictException("Hóa đơn đã được thanh toán đầy đủ.");

            var currentPaid = invoice.Payments
                .Where(p => p.Type == PaymentType.RentPayment)
                .Sum(p => p.Amount);
            var remaining = invoice.TotalAmount - currentPaid;
            if (request.Amount > remaining)
                throw new BadRequestException(
                    $"Số tiền thanh toán ({request.Amount}) vượt quá số dư còn lại ({remaining}).");

            var payment = new Payment
            {
                InvoiceId = invoice.Id,
                Type = PaymentType.RentPayment,
                Amount = request.Amount,
                PaymentMethod = "BankTransfer",
                ReferenceCode = normalizedReferenceCode,
                Note = request.Note,
                PaidAt = DateTime.UtcNow,
                RecordedBy = userId
            };
            _db.Payments.Add(payment);

            var totalPaid = currentPaid + request.Amount;
            if (totalPaid >= invoice.TotalAmount)
                invoice.Status = InvoiceStatus.Paid;
            else if (totalPaid > 0)
                invoice.Status = InvoiceStatus.PartiallyPaid;

            invoice.UpdatedAt = DateTime.UtcNow;

            _db.Notifications.Add(new Notification
            {
                UserId = invoice.Contract!.ContractTenants.First(ct => ct.IsMainTenant).TenantUserId,
                Title = "Thanh toán ghi nhận",
                Message = $"Thanh toán {request.Amount:N0}đ đã được ghi nhận cho hóa đơn tháng {invoice.BillingMonth}/{invoice.BillingYear}.",
                Type = Domain.Constants.NotificationTypes.PaymentRecorded,
                ReferenceId = invoice.Id,
            });

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var tenant = invoice.Contract!.ContractTenants.First(ct => ct.IsMainTenant).Tenant!;
            var room = invoice.Contract!.Room!;
            var (subject, html) = Application.Common.Email.EmailTemplates.PaymentRecorded(
                tenant.FullName,
                room.RoomNumber,
                room.Building!.Name,
                invoice.BillingMonth,
                invoice.BillingYear,
                request.Amount,
                invoice.TotalAmount,
                totalPaid);
            await _emailService.TrySendAsync(tenant.Email, tenant.FullName, subject, html, cancellationToken);

            return new PaymentDto
            {
                Id = payment.Id,
                InvoiceId = payment.InvoiceId,
                ContractId = payment.ContractId,
                ReservationId = payment.ReservationId,
                Type = payment.Type.ToString(),
                Amount = payment.Amount,
                PaymentMethod = payment.PaymentMethod,
                ReferenceCode = payment.ReferenceCode,
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