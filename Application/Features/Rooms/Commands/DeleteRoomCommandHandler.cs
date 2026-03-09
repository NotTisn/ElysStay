using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Rooms.Commands;

public class DeleteRoomCommandHandler : IRequestHandler<DeleteRoomCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteRoomCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteRoomCommand request, CancellationToken cancellationToken)
    {
        // Only Owner can delete
        if (!_currentUser.IsOwner)
            throw new ForbiddenException("Only the owner can delete rooms.");

        var room = await _db.Rooms
            .Include(r => r.Contracts)
            .Include(r => r.Reservations)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Room {request.Id} not found.");

        // Verify ownership through building
        var building = await _db.Buildings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == room.BuildingId, cancellationToken);

        if (building is null || building.OwnerId != _currentUser.GetRequiredUserId())
            throw new ForbiddenException("You do not own this building.");

        // SD-04: Block if active contract
        var hasActiveContract = room.Contracts.Any(c => c.Status == ContractStatus.Active);
        if (hasActiveContract)
            throw new ConflictException(
                "Cannot delete room: it has an active contract.",
                "ACTIVE_CONTRACT_EXISTS");

        // Block if pending/confirmed reservations
        var hasActiveReservation = room.Reservations
            .Any(r => r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed);
        if (hasActiveReservation)
            throw new ConflictException(
                "Cannot delete room: it has active reservations.",
                "ACTIVE_RESERVATION_EXISTS");

        room.DeletedAt = DateTime.UtcNow;
        room.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
