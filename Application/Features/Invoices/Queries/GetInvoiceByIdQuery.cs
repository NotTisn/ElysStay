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
            .Include(i => i.Contract!).ThenInclude(c => c.Room!).ThenInclude(r => r.Building!)
            .Include(i => i.Contract!).ThenInclude(c => c.TenantUser!)
            .Include(i => i.InvoiceDetails)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Invoice", request.Id);

        // Authorization
        if (_currentUser.IsTenant)
        {
            var contract = invoice.Contract!;
            var isOnContract = contract.TenantUserId == userId ||
                await _db.ContractTenants.AnyAsync(
                    ct => ct.ContractId == contract.Id && ct.TenantUserId == userId, cancellationToken);
            if (!isOnContract)
                throw new ForbiddenException("You can only view your own invoices.");
        }
        else if (_currentUser.IsOwner)
        {
            if (invoice.Contract!.Room!.Building!.OwnerId != userId)
                throw new ForbiddenException("You do not own this building.");
        }
        else if (_currentUser.IsStaff)
        {
            var isAssigned = await _db.StaffAssignments
                .AnyAsync(sa => sa.BuildingId == invoice.Contract!.Room!.BuildingId && sa.StaffId == userId, cancellationToken);
            if (!isAssigned)
                throw new ForbiddenException("You are not assigned to this building.");
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
            TenantUserId = invoice.Contract.TenantUserId,
            TenantName = invoice.Contract.TenantUser!.FullName,
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
                .ToList()
        };
    }
}
