using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Reservations.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reservations.Queries;

/// <summary>
/// GET /reservations — List reservations (paginated).
/// Auth: Owner/Staff only.
/// </summary>
public class GetReservationsQuery : PagedQuery, IRequest<PagedResult<ReservationDto>>
{
    public Guid? BuildingId { get; set; }
    public Guid? RoomId { get; set; }
    public ReservationStatus? Status { get; set; }
    public string? SortBy { get; set; }
    public bool SortDesc { get; set; }
}

public class GetReservationsQueryHandler : IRequestHandler<GetReservationsQuery, PagedResult<ReservationDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetReservationsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<ReservationDto>> Handle(GetReservationsQuery request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        var query = _db.RoomReservations
            .AsNoTracking()
            .Include(r => r.Room).ThenInclude(r => r!.Building)
            .Include(r => r.TenantUser)
            .AsQueryable();

        // Role-scope
        if (_currentUser.IsOwner)
        {
            query = query.Where(r => r.Room!.Building!.OwnerId == userId);
        }
        else if (_currentUser.IsStaff)
        {
            var assignedBuildingIds = _db.StaffAssignments
                .Where(sa => sa.StaffId == userId)
                .Select(sa => sa.BuildingId);
            query = query.Where(r => assignedBuildingIds.Contains(r.Room!.BuildingId));
        }

        // Filters
        if (request.BuildingId.HasValue)
            query = query.Where(r => r.Room!.BuildingId == request.BuildingId.Value);

        if (request.RoomId.HasValue)
            query = query.Where(r => r.RoomId == request.RoomId.Value);

        if (request.Status.HasValue)
            query = query.Where(r => r.Status == request.Status.Value);

        // Sort
        query = request.SortBy?.ToLowerInvariant() switch
        {
            "expiresat" => request.SortDesc ? query.OrderByDescending(r => r.ExpiresAt) : query.OrderBy(r => r.ExpiresAt),
            "depositamount" => request.SortDesc ? query.OrderByDescending(r => r.DepositAmount) : query.OrderBy(r => r.DepositAmount),
            "status" => request.SortDesc ? query.OrderByDescending(r => r.Status) : query.OrderBy(r => r.Status),
            _ => query.OrderByDescending(r => r.CreatedAt)
        };

        var pagedResult = await query
            .Select(r => new ReservationDto(
                r.Id,
                r.RoomId,
                r.Room!.RoomNumber,
                r.Room.BuildingId,
                r.Room.Building!.Name,
                r.TenantUserId,
                r.TenantUser != null ? r.TenantUser.FullName : null,
                r.DepositAmount,
                r.Status.ToString(),
                r.ExpiresAt,
                r.Note,
                r.RefundAmount,
                r.RefundedAt,
                r.RefundNote,
                r.CreatedAt,
                r.UpdatedAt))
            .ToPagedResultAsync(request, ct);

        return pagedResult;
    }
}
