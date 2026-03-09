using Application.Common.Exceptions;
using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.RoomServices.Commands;

/// <summary>
/// Remove a single room service override, reverting to building default.
/// </summary>
public record RemoveRoomServiceOverrideCommand(Guid RoomId, Guid ServiceId) : IRequest;

public class RemoveRoomServiceOverrideCommandHandler : IRequestHandler<RemoveRoomServiceOverrideCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IBuildingScopeService _buildingScope;

    public RemoveRoomServiceOverrideCommandHandler(IApplicationDbContext db, IBuildingScopeService buildingScope)
    {
        _db = db;
        _buildingScope = buildingScope;
    }

    public async Task Handle(RemoveRoomServiceOverrideCommand request, CancellationToken cancellationToken)
    {
        var room = await _db.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RoomId, cancellationToken)
            ?? throw new NotFoundException($"Room {request.RoomId} not found.");

        await _buildingScope.AuthorizeAsync(room.BuildingId, cancellationToken);

        var override_ = await _db.RoomServices
            .FirstOrDefaultAsync(rs => rs.RoomId == request.RoomId && rs.ServiceId == request.ServiceId, cancellationToken)
            ?? throw new NotFoundException($"No override found for service {request.ServiceId} in room {request.RoomId}.");

        _db.RoomServices.Remove(override_);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
