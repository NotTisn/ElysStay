using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Payments.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Payments.Commands;

/// <summary>
/// Batch record payments on multiple invoices.
/// PAY-06: All-or-nothing (single transaction).
/// </summary>
public record BatchRecordPaymentsCommand : IRequest<IReadOnlyList<PaymentDto>>
{
    public required IReadOnlyList<BatchPaymentEntry> Payments { get; init; }
}

public record BatchPaymentEntry
{
    public required Guid InvoiceId { get; init; }
    public required decimal Amount { get; init; }
    public string? PaymentMethod { get; init; }
    public string? Note { get; init; }
}

public class BatchRecordPaymentsCommandHandler : IRequestHandler<BatchRecordPaymentsCommand, IReadOnlyList<PaymentDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;
    private readonly IEmailService _emailService;

    public BatchRecordPaymentsCommandHandler(
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

    public async Task<IReadOnlyList<PaymentDto>> Handle(BatchRecordPaymentsCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        // Use serializable isolation to prevent concurrent overpayment (consistent with single RecordPaymentCommand)
        await using var transaction = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        try
        {

        var invoiceIds = request.Payments.Select(p => p.InvoiceId).Distinct().ToList();

        var invoices = await _db.Invoices
            .Include(i => i.Payments)
            .Include(i => i.Contract!).ThenInclude(c => c.Room!).ThenInclude(r => r.Building!)
            .Include(i => i.Contract!).ThenInclude(c => c.ContractTenants).ThenInclude(ct => ct.Tenant!)
            .Where(i => invoiceIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        if (invoices.Count != invoiceIds.Count)
            throw new NotFoundException("Một số hóa đơn không được tìm thấy.");

        // Authorize all distinct buildings
        var buildingIds = invoices.Values
            .Select(i => i.Contract!.Room!.BuildingId)
            .Distinct()
            .ToList();
        foreach (var buildingId in buildingIds)
            await _buildingScope.AuthorizeAsync(buildingId, cancellationToken);

        // Track cumulative in-batch amounts per invoice to handle duplicates correctly
        var batchCumulativeAmounts = new Dictionary<Guid, decimal>();

        var results = new List<PaymentDto>();

        foreach (var entry in request.Payments)
        {
            var invoice = invoices[entry.InvoiceId];

            // PAY-04: Cannot pay DRAFT or VOID or already PAID
            if (invoice.Status == InvoiceStatus.Draft)
                throw new ConflictException($"Không thể ghi nhận thanh toán cho hóa đơn Nháp {invoice.Id}.");
            if (invoice.Status == InvoiceStatus.Void)
                throw new ConflictException($"Không thể ghi nhận thanh toán cho hóa đơn đã hủy {invoice.Id}.");
            if (invoice.Status == InvoiceStatus.Paid)
                throw new ConflictException($"Hóa đơn {invoice.Id} đã được thanh toán đầy đủ.");

            // Overpayment guard (accounting for in-batch running total)
            var dbPaid = invoice.Payments
                .Where(p => p.Type == PaymentType.RentPayment)
                .Sum(p => p.Amount);
            batchCumulativeAmounts.TryGetValue(entry.InvoiceId, out var batchPrior);
            var totalPaidSoFar = dbPaid + batchPrior;
            var remaining = invoice.TotalAmount - totalPaidSoFar;

            if (entry.Amount > remaining)
                throw new BadRequestException(
                    $"Thanh toán {entry.Amount:N0}đ cho hóa đơn {invoice.Id} vượt quá số dư còn lại {remaining:N0}đ.");

            batchCumulativeAmounts[entry.InvoiceId] = batchPrior + entry.Amount;

            var payment = new Payment
            {
                InvoiceId = invoice.Id,
                Type = PaymentType.RentPayment,
                Amount = entry.Amount,
                PaymentMethod = entry.PaymentMethod,
                Note = entry.Note,
                PaidAt = DateTime.UtcNow,
                RecordedBy = userId
            };
            _db.Payments.Add(payment);

            // Auto-transition using running total
            var totalPaid = totalPaidSoFar + entry.Amount;

            if (totalPaid >= invoice.TotalAmount)
                invoice.Status = InvoiceStatus.Paid;
            else if (totalPaid > 0)
                invoice.Status = InvoiceStatus.PartiallyPaid;

            invoice.UpdatedAt = DateTime.UtcNow;

            var mainTenant = invoice.Contract!.ContractTenants.First(ct => ct.IsMainTenant);

            // NT-05: Notify tenant that payment was recorded
            _db.Notifications.Add(new Notification
            {
                UserId = mainTenant.TenantUserId,
                Title = "Thanh toán ghi nhận",
                Message = $"Thanh toán {entry.Amount:N0}đ đã được ghi nhận cho hóa đơn tháng {invoice.BillingMonth}/{invoice.BillingYear}.",
                Type = Domain.Constants.NotificationTypes.PaymentRecorded,
                ReferenceId = invoice.Id,
            });

            results.Add(new PaymentDto
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
            });
        }

        // PAY-06: All-or-nothing
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Best-effort emails to tenants (after successful commit)
        foreach (var result in results)
        {
            if (result.InvoiceId.HasValue && invoices.TryGetValue(result.InvoiceId.Value, out var inv))
            {
                var tenant = inv.Contract!.ContractTenants.First(ct => ct.IsMainTenant).Tenant!;
                var room = inv.Contract!.Room!;
                var totalPaid = inv.Payments.Where(p => p.Type == PaymentType.RentPayment).Sum(p => p.Amount);
                var (subject, html) = Application.Common.Email.EmailTemplates.PaymentRecorded(
                    tenant.FullName, room.RoomNumber, room.Building!.Name,
                    inv.BillingMonth, inv.BillingYear,
                    result.Amount, inv.TotalAmount, totalPaid);
                await _emailService.TrySendAsync(tenant.Email, tenant.FullName, subject, html, cancellationToken);
            }
        }

        return results;

        } // end try
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
