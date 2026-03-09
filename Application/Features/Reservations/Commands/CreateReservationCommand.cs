using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Reservations.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reservations.Commands;

/// <summary>
/// POST /reservations — Create a reservation. Room → BOOKED.
/// Auth: Owner/Staff (building-scoped).
/// SM-01: Room must be AVAILABLE → BOOKED.
/// DEP-02: Deposit recorded on reservation only — NO Payment yet.
/// </summary>
public class CreateReservationCommand : IRequest<ReservationDto>
{
    public Guid RoomId { get; set; }
    public Guid TenantUserId { get; set; }
    public decimal? DepositAmount { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Note { get; set; }
}

public class CreateReservationCommandHandler : IRequestHandler<CreateReservationCommand, ReservationDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBuildingScopeService _buildingScope;

    public CreateReservationCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IBuildingScopeService buildingScope)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }

    public async Task<ReservationDto> Handle(CreateReservationCommand request, CancellationToken ct)
    {
        var userId = _currentUser.GetRequiredUserId();

        // Load and validate room
        var room = await _db.Rooms
            .Include(r => r.Building)
            .FirstOrDefaultAsync(r => r.Id == request.RoomId && r.DeletedAt == null, ct)
            ?? throw new NotFoundException($"Room {request.RoomId} not found.");

        await _buildingScope.AuthorizeAsync(room.BuildingId, ct);

        // SM-01: Room must be AVAILABLE
        if (room.Status != RoomStatus.Available)
            throw new ConflictException(
                $"Room is currently {room.Status}. Only AVAILABLE rooms can be reserved.",
                "ROOM_NOT_AVAILABLE");

        // Validate tenant user exists and has Tenant role
        var tenantUser = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.TenantUserId, ct)
            ?? throw new NotFoundException($"User {request.TenantUserId} not found.");

        if (tenantUser.Role != UserRole.Tenant)
            throw new BadRequestException("Only users with Tenant role can be assigned to a reservation.");

        // Check tenant doesn't already have a pending/confirmed reservation
        var hasActiveReservation = await _db.RoomReservations
            .AnyAsync(r => r.TenantUserId == request.TenantUserId
                && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed), ct);
        if (hasActiveReservation)
            throw new ConflictException(
                "This tenant already has an active reservation.",
                "TENANT_HAS_ACTIVE_RESERVATION");

        // Create reservation (DEP-02: deposit on reservation only, no Payment yet)
        var reservation = new Domain.Entities.RoomReservation
        {
            RoomId = request.RoomId,
            TenantUserId = request.TenantUserId,
            DepositAmount = request.DepositAmount ?? room.Price * 0.5m, // Default: 50% of room price
            ExpiresAt = request.ExpiresAt ?? DateTime.UtcNow.AddDays(7), // Default +7 days
            Note = request.Note,
            Status = ReservationStatus.Pending
        };

        _db.RoomReservations.Add(reservation);

        // SM-01: AVAILABLE → BOOKED
        room.Status = RoomStatus.Booked;
        room.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "Room was modified by another user. Please retry.",
                "CONCURRENCY_CONFLICT");
        }

        return new ReservationDto(
            reservation.Id,
            reservation.RoomId,
            room.RoomNumber,
            room.BuildingId,
            room.Building!.Name,
            reservation.TenantUserId,
            tenantUser.FullName,
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
