using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Dashboard.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Dashboard.Queries;

/// <summary>
/// GET /users/me/dashboard — Role-based homepage summary.
/// Auth: ALL.
/// </summary>
public record GetMyDashboardQuery : IRequest<object>;

public class GetMyDashboardQueryHandler : IRequestHandler<GetMyDashboardQuery, object>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMyDashboardQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<object> Handle(GetMyDashboardQuery request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        if (_currentUser.IsOwner)
            return await GetOwnerDashboard(userId, ct);
        if (_currentUser.IsStaff)
            return await GetStaffDashboard(userId, ct);
        if (_currentUser.IsTenant)
            return await GetTenantDashboard(userId, ct);

        throw new ForbiddenException("Vai trò không xác định.");
    }

    private async Task<OwnerDashboardDto> GetOwnerDashboard(Guid userId, CancellationToken ct)
    {
        var buildings = await _db.Buildings
            .AsNoTracking()
            .Where(b => b.OwnerId == userId && b.DeletedAt == null)
            .Select(b => b.Id)
            .ToListAsync(ct);

        var totalRooms = await _db.Rooms
            .CountAsync(r => buildings.Contains(r.BuildingId) && r.DeletedAt == null, ct);

        var occupiedRooms = await _db.Rooms
            .CountAsync(r => buildings.Contains(r.BuildingId)
                && r.DeletedAt == null
                && r.Status == RoomStatus.Occupied, ct);

        var activeContracts = await _db.Contracts
            .CountAsync(c => c.Status == ContractStatus.Active
                && buildings.Contains(c.Room!.BuildingId), ct);

        // Contracts expiring within 30 days
        var expiryThreshold = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expiringContracts = await _db.Contracts
            .CountAsync(c => c.Status == ContractStatus.Active
                && buildings.Contains(c.Room!.BuildingId)
                && c.EndDate >= today
                && c.EndDate <= expiryThreshold, ct);

        // Pending reservations (Pending + Confirmed)
        var pendingReservations = await _db.RoomReservations
            .CountAsync(r => buildings.Contains(r.Room!.BuildingId)
                && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed), ct);

        var overdueInvoices = await _db.Invoices
            .Where(i => i.Status == InvoiceStatus.Overdue
                && buildings.Contains(i.Contract!.Room!.BuildingId))
            .Select(i => new { i.TotalAmount, Paid = i.Payments.Where(p => p.Type == PaymentType.RentPayment).Sum(p => p.Amount) })
            .ToListAsync(ct);

        var overdueCount = overdueInvoices.Count;
        var overdueAmount = overdueInvoices.Sum(i => i.TotalAmount - i.Paid);

        // Monthly revenue: sum of rent payments in current month
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        var monthlyRevenue = await _db.Payments
            .Where(p => p.Type == PaymentType.RentPayment
                && p.PaidAt >= monthStart
                && p.PaidAt < monthEnd
                && p.Invoice != null
                && buildings.Contains(p.Invoice.Contract!.Room!.BuildingId))
            .SumAsync(p => p.Amount, ct);

        // Pending meter readings: occupied rooms without readings for current month
        var ownerTotalOccupied = await _db.Rooms
            .CountAsync(r => buildings.Contains(r.BuildingId)
                && r.DeletedAt == null
                && r.Status == RoomStatus.Occupied, ct);

        var ownerRoomsWithReadings = await _db.MeterReadings
            .Where(mr => mr.BillingYear == now.Year && mr.BillingMonth == now.Month)
            .Where(mr => buildings.Contains(mr.Room!.BuildingId))
            .Select(mr => mr.RoomId)
            .Distinct()
            .CountAsync(ct);

        var ownerPendingReadings = Math.Max(0, ownerTotalOccupied - ownerRoomsWithReadings);

        return new OwnerDashboardDto(
            buildings.Count,
            totalRooms,
            occupiedRooms,
            totalRooms > 0 ? Math.Round((decimal)occupiedRooms / totalRooms, 2) : 0,
            activeContracts,
            expiringContracts,
            pendingReservations,
            overdueCount,
            overdueAmount,
            monthlyRevenue,
            ownerPendingReadings);
    }

    private async Task<StaffDashboardDto> GetStaffDashboard(Guid userId, CancellationToken ct)
    {
        var assignedBuildingIds = await _db.StaffAssignments
            .Where(sa => sa.StaffId == userId)
            .Select(sa => sa.BuildingId)
            .ToListAsync(ct);

        var pendingIssues = await _db.MaintenanceIssues
            .CountAsync(i => assignedBuildingIds.Contains(i.BuildingId)
                && (i.Status == IssueStatus.New || i.Status == IssueStatus.InProgress), ct);

        // Pending meter readings: rooms in assigned buildings that don't have readings for current month
        var now = DateTime.UtcNow;
        var roomsWithReadings = _db.MeterReadings
            .Where(mr => mr.BillingYear == now.Year && mr.BillingMonth == now.Month)
            .Select(mr => mr.RoomId)
            .Distinct();

        var totalActiveRooms = await _db.Rooms
            .CountAsync(r => assignedBuildingIds.Contains(r.BuildingId)
                && r.DeletedAt == null
                && r.Status == RoomStatus.Occupied, ct);

        var roomsWithReadingsCount = await _db.MeterReadings
            .Where(mr => mr.BillingYear == now.Year && mr.BillingMonth == now.Month)
            .Where(mr => assignedBuildingIds.Contains(mr.Room!.BuildingId))
            .Select(mr => mr.RoomId)
            .Distinct()
            .CountAsync(ct);

        var pendingReadings = totalActiveRooms - roomsWithReadingsCount;
        if (pendingReadings < 0) pendingReadings = 0;

        return new StaffDashboardDto(
            assignedBuildingIds.Count,
            pendingIssues,
            pendingReadings);
    }

    private async Task<TenantDashboardDto> GetTenantDashboard(Guid userId, CancellationToken ct)
    {
        // Find active contract (where user is main or roommate)
        var activeContract = await _db.Contracts
            .AsNoTracking()
            .Include(c => c.Room).ThenInclude(r => r!.Building)
            .Where(c => c.Status == ContractStatus.Active)
            .Where(c => c.ContractTenants.Any(t => t.TenantUserId == userId && t.MoveOutDate == null))
            .FirstOrDefaultAsync(ct);

        if (activeContract is null)
        {
            return new TenantDashboardDto(null, null, null, null, null, 0, 0m, 0);
        }

        // Unpaid invoices for this contract
        var unpaidInvoices = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.ContractId == activeContract.Id)
            .Where(i => i.Status == InvoiceStatus.Sent || i.Status == InvoiceStatus.PartiallyPaid || i.Status == InvoiceStatus.Overdue)
            .Select(i => new
            {
                i.TotalAmount,
                Paid = i.Payments.Where(p => p.Type == PaymentType.RentPayment).Sum(p => p.Amount)
            })
            .ToListAsync(ct);

        var unpaidCount = unpaidInvoices.Count;
        var unpaidAmount = unpaidInvoices.Sum(i => i.TotalAmount - i.Paid);

        // Open issues reported by this tenant
        var openIssues = await _db.MaintenanceIssues
            .CountAsync(i => i.ReportedBy == userId
                && (i.Status == IssueStatus.New || i.Status == IssueStatus.InProgress), ct);

        return new TenantDashboardDto(
            activeContract.RoomId,
            activeContract.Room!.RoomNumber,
            activeContract.Room.Building!.Name,
            activeContract.Status.ToString(),
            activeContract.EndDate,
            unpaidCount,
            unpaidAmount,
            openIssues);
    }
}
