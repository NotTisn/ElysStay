using Application.Common.Interfaces;
using Application.Features.Payments.DTOs;
using Domain.Enums;
using MediatR;
using Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Payments.Queries;

/// <summary>
/// Returns aggregate totals for payments using the same scope and filters as the paged list.
/// </summary>
public record GetPaymentSummaryQuery : IRequest<PaymentSummaryDto>
{
    public Guid? BuildingId { get; init; }
    public string? Type { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
}

public class GetPaymentSummaryQueryHandler : IRequestHandler<GetPaymentSummaryQuery, PaymentSummaryDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetPaymentSummaryQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PaymentSummaryDto> Handle(GetPaymentSummaryQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var query = _db.Payments.AsNoTracking().AsQueryable();

        if (_currentUser.IsOwner)
        {
            query = query.Where(p =>
                (p.Invoice != null && p.Invoice.Contract!.Room!.Building!.OwnerId == userId) ||
                (p.Contract != null && p.Contract.Room!.Building!.OwnerId == userId) ||
                (p.Reservation != null && p.Reservation.Room!.Building!.OwnerId == userId));
        }
        else if (_currentUser.IsStaff)
        {
            query = query.Where(p =>
                (p.Invoice != null && p.Invoice.Contract!.Room!.Building!.BuildingStaffs.Any(bs => bs.StaffId == userId)) ||
                (p.Contract != null && p.Contract.Room!.Building!.BuildingStaffs.Any(bs => bs.StaffId == userId)) ||
                (p.Reservation != null && p.Reservation.Room!.Building!.BuildingStaffs.Any(bs => bs.StaffId == userId)));
        }
        else if (_currentUser.IsTenant)
        {
            query = query.Where(p =>
                (p.Invoice != null && (p.Invoice.Contract!.TenantUserId == userId ||
                    p.Invoice.Contract!.ContractTenants.Any(ct => ct.TenantUserId == userId))) ||
                (p.Contract != null && (p.Contract.TenantUserId == userId ||
                    p.Contract.ContractTenants.Any(ct => ct.TenantUserId == userId))) ||
                (p.Reservation != null && p.Reservation.TenantUserId == userId));
        }
        else
        {
            throw new ForbiddenException("Current role is not allowed to access payment summaries.");
        }

        if (request.BuildingId.HasValue)
        {
            query = query.Where(p =>
                (p.Invoice != null && p.Invoice.Contract!.Room!.BuildingId == request.BuildingId.Value) ||
                (p.Contract != null && p.Contract.Room!.BuildingId == request.BuildingId.Value) ||
                (p.Reservation != null && p.Reservation.Room!.BuildingId == request.BuildingId.Value));
        }

        if (!string.IsNullOrWhiteSpace(request.Type) && Enum.TryParse<PaymentType>(request.Type, true, out var paymentType))
        {
            query = query.Where(p => p.Type == paymentType);
        }

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

        var aggregates = await query
            .GroupBy(_ => 1)
            .Select(group => new PaymentSummaryDto(
                group.Sum(p => p.Type == PaymentType.DepositRefund ? -p.Amount : p.Amount),
                group.Where(p => p.Type == PaymentType.RentPayment).Sum(p => p.Amount),
                group.Where(p => p.Type == PaymentType.DepositIn).Sum(p => p.Amount),
                group.Where(p => p.Type == PaymentType.DepositRefund).Sum(p => p.Amount),
                group.Count()))
            .FirstOrDefaultAsync(cancellationToken);

        return aggregates ?? new PaymentSummaryDto(0, 0, 0, 0, 0);
    }
}