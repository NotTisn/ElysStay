using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Invoices.Commands;

/// <summary>
/// Sends an invoice: DRAFT → SENT.
/// SM-11.
/// </summary>
public record SendInvoiceCommand(Guid Id) : IRequest<Unit>;

public class SendInvoiceCommandHandler : IRequestHandler<SendInvoiceCommand, Unit>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;

    public SendInvoiceCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IBuildingScopeService buildingScope)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }

    public async Task<Unit> Handle(SendInvoiceCommand request, CancellationToken cancellationToken)
    {
        _currentUser.GetRequiredUserId();

        var invoice = await _db.Invoices
            .Include(i => i.Contract!).ThenInclude(c => c.Room!)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Invoice", request.Id);

        if (invoice.Status != InvoiceStatus.Draft)
            throw new ConflictException("Only DRAFT invoices can be sent.");

        await _buildingScope.AuthorizeAsync(invoice.Contract!.Room!.BuildingId, cancellationToken);

        invoice.Status = InvoiceStatus.Sent;
        invoice.UpdatedAt = DateTime.UtcNow;

        // NT-01: Notify tenant about new invoice
        _db.Notifications.Add(new Domain.Entities.Notification
        {
            UserId = invoice.Contract!.TenantUserId,
            Title = "Hóa đơn mới",
            Message = $"Hóa đơn tháng {invoice.BillingMonth}/{invoice.BillingYear} đã được gửi.",
            Type = "INVOICE_SENT",
            ReferenceId = invoice.Id,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
