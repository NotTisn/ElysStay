using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Payments.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Payments.Queries;

/// <summary>
/// Lists payment history with filters and pagination.
/// Owner/Staff see all in their buildings. Tenant sees own.
/// </summary>
public record GetPaymentsQuery : IRequest<PagedResult<PaymentDto>>
{
    public Guid? BuildingId { get; init; }
    public string? Type { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string Sort { get; init; } = "paidAt:desc";
}

public class GetPaymentsQueryHandler : IRequestHandler<GetPaymentsQuery, PagedResult<PaymentDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetPaymentsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<PaymentDto>> Handle(GetPaymentsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var query = _db.Payments.AsNoTracking().AsQueryable();

        // Scope by role
        if (_currentUser.IsOwner)
        {
            query = query.Where(p =>
                (p.Invoice != null && p.Invoice.Contract!.Room!.Building!.OwnerId == userId) ||
                (p.Contract != null && p.Contract.Room!.Building!.OwnerId == userId));
        }
        else if (_currentUser.IsStaff)
        {
            query = query.Where(p =>
                (p.Invoice != null && p.Invoice.Contract!.Room!.Building!.BuildingStaffs.Any(bs => bs.StaffId == userId)) ||
                (p.Contract != null && p.Contract.Room!.Building!.BuildingStaffs.Any(bs => bs.StaffId == userId)));
        }
        else if (_currentUser.IsTenant)
        {
            query = query.Where(p =>
                (p.Invoice != null && (p.Invoice.Contract!.TenantUserId == userId ||
                    p.Invoice.Contract!.ContractTenants.Any(ct => ct.TenantUserId == userId))) ||
                (p.Contract != null && (p.Contract.TenantUserId == userId ||
                    p.Contract.ContractTenants.Any(ct => ct.TenantUserId == userId))));
        }

        // Filters
        if (request.BuildingId.HasValue)
        {
            query = query.Where(p =>
                (p.Invoice != null && p.Invoice.Contract!.Room!.BuildingId == request.BuildingId.Value) ||
                (p.Contract != null && p.Contract.Room!.BuildingId == request.BuildingId.Value));
        }

        if (!string.IsNullOrWhiteSpace(request.Type) && Enum.TryParse<PaymentType>(request.Type, true, out var paymentType))
            query = query.Where(p => p.Type == paymentType);

        if (request.FromDate.HasValue)
        {
            var from = request.FromDate.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(p => p.PaidAt >= from);
        }

        if (request.ToDate.HasValue)
        {
            var to = request.ToDate.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(p => p.PaidAt <= to);
        }

        // Sort
        query = ApplySort(query, request.Sort);

        var paging = new PagedQuery { Page = request.Page, PageSize = request.PageSize };

        return await query
            .Select(p => new PaymentDto
            {
                Id = p.Id,
                InvoiceId = p.InvoiceId,
                ContractId = p.ContractId,
                Type = p.Type.ToString(),
                Amount = p.Amount,
                PaymentMethod = p.PaymentMethod,
                Note = p.Note,
                PaidAt = p.PaidAt,
                RecordedBy = p.RecordedBy,
                RecorderName = p.Recorder!.FullName,
                CreatedAt = p.CreatedAt
            })
            .ToPagedResultAsync(paging, cancellationToken);
    }

    private static IQueryable<Domain.Entities.Payment> ApplySort(IQueryable<Domain.Entities.Payment> query, string sort)
    {
        var parts = sort.Split(':');
        var field = parts[0].ToLowerInvariant();
        var desc = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        return field switch
        {
            "amount" => desc ? query.OrderByDescending(p => p.Amount) : query.OrderBy(p => p.Amount),
            "type" => desc ? query.OrderByDescending(p => p.Type) : query.OrderBy(p => p.Type),
            _ => desc ? query.OrderByDescending(p => p.PaidAt) : query.OrderBy(p => p.PaidAt)
        };
    }
}
