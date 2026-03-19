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
    private readonly IBuildingScopeService _buildingScope;

    public DeleteRoomCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IBuildingScopeService buildingScope)
    {
        _db = db;
        _currentUser = currentUser;
        _buildingScope = buildingScope;
    }

    public async Task Handle(DeleteRoomCommand request, CancellationToken cancellationToken)
    {
        // Only Owner can delete
        if (!_currentUser.IsOwner)
            throw new ForbiddenException("Chỉ chủ nhà mới có thể xóa phòng.");

        var room = await _db.Rooms
            .FirstOrDefaultAsync(r => r.Id == request.Id && r.DeletedAt == null, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy phòng {request.Id}.");

        // Building scope auth — consistent with CreateRoom/UpdateRoom
        await _buildingScope.AuthorizeAsync(room.BuildingId, cancellationToken);

        // SD-04: Block if active contract
        var hasActiveContract = await _db.Contracts
            .AnyAsync(c => c.RoomId == room.Id && c.Status == ContractStatus.Active, cancellationToken);
        if (hasActiveContract)
            throw new ConflictException(
                "Cannot delete room: it has an active contract.",
                "ACTIVE_CONTRACT_EXISTS");

        // Block if pending/confirmed reservations
        var hasActiveReservation = await _db.RoomReservations
            .AnyAsync(r => r.RoomId == room.Id
                && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed), cancellationToken);
        if (hasActiveReservation)
            throw new ConflictException(
                "Cannot delete room: it has active reservations.",
                "ACTIVE_RESERVATION_EXISTS");

        room.DeletedAt = DateTime.UtcNow;
        room.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
