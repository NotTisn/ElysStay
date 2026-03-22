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
    private readonly IEmailService _emailService;

    public SendInvoiceCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IBuildingScopeService buildingScope, IEmailService emailService)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
        _emailService = emailService;
    }

    public async Task<Unit> Handle(SendInvoiceCommand request, CancellationToken cancellationToken)
    {
        _currentUser.GetRequiredUserId();

        var invoice = await _db.Invoices
            .Include(i => i.Contract!).ThenInclude(c => c.Room!).ThenInclude(r => r.Building!)
            .Include(i => i.Contract!).ThenInclude(c => c.TenantUser!)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Hóa đơn", request.Id);

        if (invoice.Status != InvoiceStatus.Draft)
            throw new ConflictException("Chỉ có thể gửi hóa đơn ở trạng thái Nháp.");

        await _buildingScope.AuthorizeAsync(invoice.Contract!.Room!.BuildingId, cancellationToken);

        invoice.Status = InvoiceStatus.Sent;
        invoice.UpdatedAt = DateTime.UtcNow;

        // NT-01: Notify tenant about new invoice
        _db.Notifications.Add(new Domain.Entities.Notification
        {
            UserId = invoice.Contract!.TenantUserId,
            Title = "Hóa đơn mới",
            Message = $"Hóa đơn tháng {invoice.BillingMonth}/{invoice.BillingYear} đã được gửi.",
            Type = Domain.Constants.NotificationTypes.InvoiceSent,
            ReferenceId = invoice.Id,
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Hóa đơn đã bị thay đổi bởi thao tác khác. Vui lòng tải lại và thử lại.");
        }

        // Best-effort email to tenant (after successful save)
        var tenant = invoice.Contract!.TenantUser!;
        var room = invoice.Contract!.Room!;
        var (subject, html) = Application.Common.Email.EmailTemplates.InvoiceSent(
            tenant.FullName, room.RoomNumber, room.Building!.Name,
            invoice.BillingMonth, invoice.BillingYear, invoice.TotalAmount, invoice.DueDate);
        await _emailService.TrySendAsync(tenant.Email, tenant.FullName, subject, html, cancellationToken);

        return Unit.Value;
    }
}
