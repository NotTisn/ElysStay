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

    public BatchRecordPaymentsCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }

    public async Task<IReadOnlyList<PaymentDto>> Handle(BatchRecordPaymentsCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var invoiceIds = request.Payments.Select(p => p.InvoiceId).Distinct().ToList();

        var invoices = await _db.Invoices
            .Include(i => i.Payments)
            .Include(i => i.Contract!).ThenInclude(c => c.Room!)
            .Where(i => invoiceIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        if (invoices.Count != invoiceIds.Count)
            throw new NotFoundException("Some invoices were not found.");

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
                throw new ConflictException($"Cannot record payment on DRAFT invoice {invoice.Id}.");
            if (invoice.Status == InvoiceStatus.Void)
                throw new ConflictException($"Cannot record payment on VOID invoice {invoice.Id}.");
            if (invoice.Status == InvoiceStatus.Paid)
                throw new ConflictException($"Invoice {invoice.Id} is already fully paid.");

            // Overpayment guard (accounting for in-batch running total)
            var dbPaid = invoice.Payments
                .Where(p => p.Type == PaymentType.RentPayment)
                .Sum(p => p.Amount);
            batchCumulativeAmounts.TryGetValue(entry.InvoiceId, out var batchPrior);
            var totalPaidSoFar = dbPaid + batchPrior;
            var remaining = invoice.TotalAmount - totalPaidSoFar;

            if (entry.Amount > remaining)
                throw new BadRequestException(
                    $"Payment {entry.Amount} on invoice {invoice.Id} exceeds remaining balance {remaining}.");

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

        return results;
    }
}
