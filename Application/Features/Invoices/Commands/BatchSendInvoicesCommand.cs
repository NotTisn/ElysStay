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
    private readonly IEmailService _emailService;

    public BatchSendInvoicesCommandHandler(
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

    public async Task<int> Handle(BatchSendInvoicesCommand request, CancellationToken cancellationToken)
    {
        _currentUser.GetRequiredUserId();

        var distinctIds = request.InvoiceIds.Distinct().ToList();

        var invoices = await _db.Invoices
            .Include(i => i.Contract!).ThenInclude(c => c.Room!).ThenInclude(r => r.Building!)
            .Include(i => i.Contract!).ThenInclude(c => c.ContractTenants).ThenInclude(ct => ct.IsMainTenant)
            .Where(i => distinctIds.Contains(i.Id))
            .ToListAsync(cancellationToken);

        if (invoices.Count != distinctIds.Count)
            throw new NotFoundException("Một số hóa đơn không được tìm thấy.");

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
                continue; 

            invoice.Status = InvoiceStatus.Sent;
            invoice.UpdatedAt = DateTime.UtcNow;
            sentCount++;

            // NT-01: Notify tenant (matching SendInvoiceCommand behavior)
            _db.Notifications.Add(new Notification
            {
                UserId = invoice.Contract!.ContractTenants.First(ct => ct.IsMainTenant).TenantUserId,
                Title = "Hóa đơn mới",
                Message = $"Hóa đơn tháng {invoice.BillingMonth}/{invoice.BillingYear} đã được gửi.",
                Type = Domain.Constants.NotificationTypes.InvoiceSent,
                ReferenceId = invoice.Id,
            });
        }

        if (sentCount > 0)
            await _db.SaveChangesAsync(cancellationToken);

        // Best-effort emails to tenants (after successful save)
        foreach (var invoice in invoices.Where(i => i.Status == InvoiceStatus.Sent))
        {
            var mainTenant = invoice.Contract!.ContractTenants.First(ct => ct.IsMainTenant) ?? throw new InvalidOperationException("Hợp đồng phải có một người thuê chính.");
            var tenant = mainTenant.Tenant!;
            var room = invoice.Contract!.Room!;
            var (subject, html) = Application.Common.Email.EmailTemplates.InvoiceSent(
                tenant.FullName, room.RoomNumber, room.Building!.Name,
                invoice.BillingMonth, invoice.BillingYear, invoice.TotalAmount, invoice.DueDate);
            await _emailService.TrySendAsync(tenant.Email, tenant.FullName, subject, html, cancellationToken);
        }

        return sentCount;
    }
}
