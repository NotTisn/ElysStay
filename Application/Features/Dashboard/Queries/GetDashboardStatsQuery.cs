using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Dashboard.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Dashboard.Queries;

/// <summary>
/// GET /reports/dashboard-stats — Overview stats. Owner/Staff only.
/// </summary>
public class GetDashboardStatsQuery : IRequest<DashboardStatsDto>
{
    public Guid? BuildingId { get; set; }
}

public class GetDashboardStatsQueryHandler : IRequestHandler<GetDashboardStatsQuery, DashboardStatsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetDashboardStatsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<DashboardStatsDto> Handle(GetDashboardStatsQuery request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        // Build base building filter
        IQueryable<Guid> buildingIds;

        if (_currentUser.IsOwner)
        {
            buildingIds = _db.Buildings
                .Where(b => b.OwnerId == userId && b.DeletedAt == null)
                .Select(b => b.Id);
        }
        else if (_currentUser.IsStaff)
        {
            buildingIds = _db.StaffAssignments
                .Where(sa => sa.StaffId == userId)
                .Select(sa => sa.BuildingId);
        }
        else
        {
            throw new ForbiddenException("Only Owner and Staff can access dashboard stats.");
        }

        // Filter to specific building if requested
        if (request.BuildingId.HasValue)
            buildingIds = buildingIds.Where(id => id == request.BuildingId.Value);

        var buildingIdList = await buildingIds.ToListAsync(ct);

        var totalRooms = await _db.Rooms
            .CountAsync(r => buildingIdList.Contains(r.BuildingId) && r.DeletedAt == null, ct);

        var occupiedRooms = await _db.Rooms
            .CountAsync(r => buildingIdList.Contains(r.BuildingId)
                && r.DeletedAt == null
                && r.Status == RoomStatus.Occupied, ct);

        var activeContracts = await _db.Contracts
            .CountAsync(c => c.Status == ContractStatus.Active
                && buildingIdList.Contains(c.Room!.BuildingId), ct);

        // SM-10: Contracts past EndDate that are still ACTIVE
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var overdueContracts = await _db.Contracts
            .CountAsync(c => c.Status == ContractStatus.Active
                && c.EndDate < today
                && buildingIdList.Contains(c.Room!.BuildingId), ct);

        // Overdue invoices
        var overdueInvoiceData = await _db.Invoices
            .Where(i => i.Status == InvoiceStatus.Overdue
                && buildingIdList.Contains(i.Contract!.Room!.BuildingId))
            .Select(i => new
            {
                i.TotalAmount,
                Paid = i.Payments.Where(p => p.Type == PaymentType.RentPayment).Sum(p => p.Amount)
            })
            .ToListAsync(ct);

        var overdueInvoiceCount = overdueInvoiceData.Count;
        var overdueAmount = overdueInvoiceData.Sum(i => i.TotalAmount - i.Paid);

        // Monthly revenue
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        var monthlyRevenue = await _db.Payments
            .Where(p => p.Type == PaymentType.RentPayment
                && p.PaidAt >= monthStart
                && p.PaidAt < monthEnd
                && p.Invoice != null
                && buildingIdList.Contains(p.Invoice.Contract!.Room!.BuildingId))
            .SumAsync(p => p.Amount, ct);

        return new DashboardStatsDto(
            totalRooms,
            occupiedRooms,
            totalRooms > 0 ? Math.Round((decimal)occupiedRooms / totalRooms, 2) : 0,
            activeContracts,
            overdueContracts,
            overdueInvoiceCount,
            overdueAmount,
            monthlyRevenue);
    }
}
