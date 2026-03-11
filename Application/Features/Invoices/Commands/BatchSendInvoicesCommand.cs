using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Invoices.Commands;

/// <summary>
/// Batch send multiple invoices: DRAFT → SENT.
/// </summary>
public record BatchSendInvoicesCommand : IRequest<int>
{
    public required IReadOnlyList<Guid> InvoiceIds { get; init; }
}

public class BatchSendInvoicesCommandHandler : IRequestHandler<BatchSendInvoicesCommand, int>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;

    public BatchSendInvoicesCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }

    public async Task<int> Handle(BatchSendInvoicesCommand request, CancellationToken cancellationToken)
    {
        _currentUser.GetRequiredUserId();

        var invoices = await _db.Invoices
            .Include(i => i.Contract!).ThenInclude(c => c.Room!).ThenInclude(r => r.Building!)
            .Where(i => request.InvoiceIds.Contains(i.Id))
            .ToListAsync(cancellationToken);

        if (invoices.Count != request.InvoiceIds.Count)
            throw new NotFoundException("Some invoices were not found.");

        // Authorize all distinct buildings
        var buildingIds = invoices
            .Select(i => i.Contract!.Room!.BuildingId)
            .Distinct()
            .ToList();
        foreach (var buildingId in buildingIds)
            await _buildingScope.AuthorizeAsync(buildingId, cancellationToken);

        var sentCount = 0;
        foreach (var invoice in invoices)
        {
            if (invoice.Status != InvoiceStatus.Draft)
                continue; // Skip non-draft invoices silently in batch

            invoice.Status = InvoiceStatus.Sent;
            invoice.UpdatedAt = DateTime.UtcNow;
            sentCount++;

            // NT-01: Notify tenant (matching SendInvoiceCommand behavior)
            _db.Notifications.Add(new Notification
            {
                UserId = invoice.Contract!.TenantUserId,
                Title = "Hóa đơn mới",
                Message = $"Hóa đơn tháng {invoice.BillingMonth}/{invoice.BillingYear} đã được gửi.",
                Type = "INVOICE_SENT",
                ReferenceId = invoice.Id,
            });
        }

        if (sentCount > 0)
            await _db.SaveChangesAsync(cancellationToken);

        return sentCount;
    }
}
