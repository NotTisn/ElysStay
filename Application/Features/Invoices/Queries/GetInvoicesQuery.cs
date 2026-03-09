using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Invoices.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Invoices.Queries;

/// <summary>
/// Lists invoices with filters and pagination.
/// All roles can access; Tenant auto-filtered.
/// </summary>
public record GetInvoicesQuery : IRequest<PagedResult<InvoiceDto>>
{
    public Guid? BuildingId { get; init; }
    public int? BillingYear { get; init; }
    public int? BillingMonth { get; init; }
    public string? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string Sort { get; init; } = "createdAt:desc";
}

public class GetInvoicesQueryHandler : IRequestHandler<GetInvoicesQuery, PagedResult<InvoiceDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetInvoicesQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<InvoiceDto>> Handle(GetInvoicesQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var query = _db.Invoices.AsNoTracking().AsQueryable();

        // Scope by role
        if (_currentUser.IsOwner)
        {
            query = query.Where(i => i.Contract!.Room!.Building!.OwnerId == userId);
        }
        else if (_currentUser.IsStaff)
        {
            query = query.Where(i => i.Contract!.Room!.Building!.BuildingStaffs.Any(bs => bs.StaffId == userId));
        }
        else if (_currentUser.IsTenant)
        {
            query = query.Where(i =>
                i.Contract!.TenantUserId == userId ||
                i.Contract!.ContractTenants.Any(ct => ct.TenantUserId == userId));
        }

        // Filters
        if (request.BuildingId.HasValue)
            query = query.Where(i => i.Contract!.Room!.BuildingId == request.BuildingId.Value);

        if (request.BillingYear.HasValue)
            query = query.Where(i => i.BillingYear == request.BillingYear.Value);

        if (request.BillingMonth.HasValue)
            query = query.Where(i => i.BillingMonth == request.BillingMonth.Value);

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<InvoiceStatus>(request.Status, true, out var status))
            query = query.Where(i => i.Status == status);

        // Sort
        query = ApplySort(query, request.Sort);

        var paging = new PagedQuery { Page = request.Page, PageSize = request.PageSize };

        return await query
            .Select(i => new InvoiceDto
            {
                Id = i.Id,
                ContractId = i.ContractId,
                RoomId = i.Contract!.RoomId,
                RoomNumber = i.Contract.Room!.RoomNumber,
                BuildingId = i.Contract.Room.BuildingId,
                BuildingName = i.Contract.Room.Building!.Name,
                TenantUserId = i.Contract.TenantUserId,
                TenantName = i.Contract.TenantUser!.FullName,
                BillingYear = i.BillingYear,
                BillingMonth = i.BillingMonth,
                RentAmount = i.RentAmount,
                ServiceAmount = i.ServiceAmount,
                PenaltyAmount = i.PenaltyAmount,
                DiscountAmount = i.DiscountAmount,
                TotalAmount = i.TotalAmount,
                PaidAmount = i.Payments.Where(p => p.Type == PaymentType.RentPayment).Sum(p => p.Amount),
                Status = i.Status.ToString(),
                DueDate = i.DueDate,
                Note = i.Note,
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt
            })
            .ToPagedResultAsync(paging, cancellationToken);
    }

    private static IQueryable<Domain.Entities.Invoice> ApplySort(IQueryable<Domain.Entities.Invoice> query, string sort)
    {
        var parts = sort.Split(':');
        var field = parts[0].ToLowerInvariant();
        var desc = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        return field switch
        {
            "billingperiod" => desc
                ? query.OrderByDescending(i => i.BillingYear).ThenByDescending(i => i.BillingMonth)
                : query.OrderBy(i => i.BillingYear).ThenBy(i => i.BillingMonth),
            "totalamount" => desc ? query.OrderByDescending(i => i.TotalAmount) : query.OrderBy(i => i.TotalAmount),
            "duedate" => desc ? query.OrderByDescending(i => i.DueDate) : query.OrderBy(i => i.DueDate),
            "status" => desc ? query.OrderByDescending(i => i.Status) : query.OrderBy(i => i.Status),
            _ => desc ? query.OrderByDescending(i => i.CreatedAt) : query.OrderBy(i => i.CreatedAt)
        };
    }
}
