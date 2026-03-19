using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Invoices.Commands;

/// <summary>
/// Voids an invoice. OWNER only.
/// SM-12: Any status except PAID → VOID.
/// Voided invoices excluded from PnL.
/// </summary>
public record VoidInvoiceCommand(Guid Id) : IRequest<Unit>;

public class VoidInvoiceCommandHandler : IRequestHandler<VoidInvoiceCommand, Unit>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;

    public VoidInvoiceCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }

    public async Task<Unit> Handle(VoidInvoiceCommand request, CancellationToken cancellationToken)
    {
        _currentUser.GetRequiredUserId();

        if (!_currentUser.IsOwner)
            throw new ForbiddenException("Chỉ chủ nhà mới có thể hủy hóa đơn.");

        var invoice = await _db.Invoices
            .Include(i => i.Contract!).ThenInclude(c => c.Room!).ThenInclude(r => r.Building!)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Hóa đơn", request.Id);

        // Use building scope service for consistent auth
        await _buildingScope.AuthorizeAsync(invoice.Contract!.Room!.BuildingId, cancellationToken);

        // SM-12: Any status except PAID → VOID (PartiallyPaid allowed — owner accepts write-off)
        if (invoice.Status == InvoiceStatus.Paid)
            throw new ConflictException("Không thể hủy hóa đơn đã thanh toán đầy đủ.");

        if (invoice.Status == InvoiceStatus.Void)
            throw new ConflictException("Hóa đơn đã bị hủy.");

        invoice.Status = InvoiceStatus.Void;
        invoice.UpdatedAt = DateTime.UtcNow;

        // NT-02: Notify tenant that their invoice was voided
        _db.Notifications.Add(new Domain.Entities.Notification
        {
            UserId = invoice.Contract!.TenantUserId,
            Title = "Hóa đơn đã hủy",
            Message = $"Hóa đơn tháng {invoice.BillingMonth}/{invoice.BillingYear} đã bị hủy.",
            Type = "INVOICE_VOIDED",
            ReferenceId = invoice.Id,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
