using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Reservations.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reservations.Queries;

/// <summary>
/// GET /reservations/{id} — Get a single reservation by ID.
/// Auth: Owner/Staff only. Staff building-scoped (AUTH-05).
/// </summary>
public record GetReservationByIdQuery(Guid Id) : IRequest<ReservationDto>;

public class GetReservationByIdQueryHandler : IRequestHandler<GetReservationByIdQuery, ReservationDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetReservationByIdQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ReservationDto> Handle(GetReservationByIdQuery request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        var reservation = await _db.RoomReservations
            .AsNoTracking()
            .Include(r => r.Room).ThenInclude(r => r!.Building)
            .Include(r => r.TenantUser)
            .FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new NotFoundException("RoomReservation", request.Id);

        // Role-scoped authorization
        if (_currentUser.IsOwner)
        {
            if (reservation.Room!.Building!.OwnerId != userId)
                throw new ForbiddenException("You do not own this building.");
        }
        else if (_currentUser.IsStaff)
        {
            var isAssigned = await _db.StaffAssignments
                .AnyAsync(sa => sa.BuildingId == reservation.Room!.BuildingId && sa.StaffId == userId, ct);
            if (!isAssigned)
                throw new ForbiddenException("You are not assigned to this building.");
        }
        else
        {
            // Tenants cannot view reservations (Owner/Staff only feature)
            throw new ForbiddenException("Tenants cannot access reservations.");
        }

        return new ReservationDto(
            reservation.Id,
            reservation.RoomId,
            reservation.Room!.RoomNumber,
            reservation.Room.BuildingId,
            reservation.Room.Building!.Name,
            reservation.TenantUserId,
            reservation.TenantUser?.FullName,
            reservation.DepositAmount,
            reservation.Status.ToString(),
            reservation.ExpiresAt,
            reservation.Note,
            reservation.RefundAmount,
            reservation.RefundedAt,
            reservation.RefundNote,
            reservation.CreatedAt,
            reservation.UpdatedAt);
    }
}
