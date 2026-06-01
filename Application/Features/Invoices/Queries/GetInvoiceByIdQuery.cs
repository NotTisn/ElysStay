using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Invoices.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Invoices.Queries;

/// <summary>
/// Gets a single invoice with line items.
/// PAY-01: PaidAmount computed from SUM(Payment.Amount).
/// </summary>
public record GetInvoiceByIdQuery(Guid Id) : IRequest<InvoiceDetailDto>;

public class GetInvoiceByIdQueryHandler : IRequestHandler<GetInvoiceByIdQuery, InvoiceDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetInvoiceByIdQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<InvoiceDetailDto> Handle(GetInvoiceByIdQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var invoice = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Contract!).ThenInclude(c => c.Room!).ThenInclude(r => r.Building!).ThenInclude(b => b.BuildingStaffs)
            .Include(i => i.Contract!).ThenInclude(c => c.ContractTenants).ThenInclude(ct => ct.Tenant!)
            .Include(i => i.InvoiceDetails)
            .Include(i => i.Payments).ThenInclude(p => p.Recorder!)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Hóa đơn", request.Id);

        // Authorization
        if (_currentUser.IsTenant)
        {
            var isOnContract = invoice.Contract!.ContractTenants.Any(ct => ct.TenantUserId == userId);
            if (!isOnContract)
                throw new ForbiddenException("Bạn chỉ có thể xem hóa đơn của mình.");
        }
        else if (_currentUser.IsOwner)
        {
            if (invoice.Contract!.Room!.Building!.OwnerId != userId)
                throw new ForbiddenException("Bạn không sở hữu tòa nhà này.");
        }
        else if (_currentUser.IsStaff)
        {
            var isAssigned = invoice.Contract!.Room!.Building!.BuildingStaffs.Any(bs => bs.StaffId == userId);
            if (!isAssigned)
                throw new ForbiddenException("Bạn không được phân công cho tòa nhà này.");
        }

        var paidAmount = invoice.Payments
            .Where(p => p.Type == PaymentType.RentPayment)
            .Sum(p => p.Amount);

        return new InvoiceDetailDto
        {
            Id = invoice.Id,
            ContractId = invoice.ContractId,
            RoomId = invoice.Contract!.RoomId,
            RoomNumber = invoice.Contract.Room!.RoomNumber,
            BuildingId = invoice.Contract.Room.BuildingId,
            BuildingName = invoice.Contract.Room.Building!.Name,
            TenantUserId = invoice.Contract.ContractTenants.FirstOrDefault(ct => ct.IsMainTenant)?.TenantUserId
                ?? throw new InvalidOperationException($"Hợp đồng {invoice.ContractId} không có người thuê chính."),
            TenantName = invoice.Contract.ContractTenants.First(ct => ct.IsMainTenant).Tenant!.FullName,
            BillingYear = invoice.BillingYear,
            BillingMonth = invoice.BillingMonth,
            RentAmount = invoice.RentAmount,
            ServiceAmount = invoice.ServiceAmount,
            PenaltyAmount = invoice.PenaltyAmount,
            DiscountAmount = invoice.DiscountAmount,
            TotalAmount = invoice.TotalAmount,
            PaidAmount = paidAmount,
            Status = invoice.Status.ToString(),
            DueDate = invoice.DueDate,
            Note = invoice.Note,
            CreatedAt = invoice.CreatedAt,
            UpdatedAt = invoice.UpdatedAt,
            LineItems = invoice.InvoiceDetails
                .OrderBy(d => d.ServiceId == null ? 0 : 1)  // Rent line first
                .ThenBy(d => d.Description)
                .Select(d => new InvoiceLineItemDto
                {
                    Id = d.Id,
                    ServiceId = d.ServiceId,
                    Description = d.Description,
                    Quantity = d.Quantity,
                    UnitPrice = d.UnitPrice,
                    Amount = d.Amount,
                    PreviousReading = d.PreviousReading,
                    CurrentReading = d.CurrentReading
                })
                .ToList(),
            Payments = invoice.Payments
                .OrderByDescending(p => p.PaidAt)
                .Select(p => new InvoicePaymentDto
                {
                    Id = p.Id,
                    Amount = p.Amount,
                    Type = p.Type.ToString(),
                    PaymentMethod = p.PaymentMethod,
                    PaidAt = p.PaidAt,
                    ReferenceCode = p.ReferenceCode,
                    Note = p.Note,
                    RecordedByName = p.Recorder?.FullName ?? "Unknown"
                })
                .ToList()
        };
    }
}
