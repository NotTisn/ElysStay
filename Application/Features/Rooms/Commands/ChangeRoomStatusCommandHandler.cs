using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Rooms.DTOs;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Rooms.Commands;

public class ChangeRoomStatusCommandHandler : IRequestHandler<ChangeRoomStatusCommand, RoomDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IBuildingScopeService _buildingScope;

    public ChangeRoomStatusCommandHandler(IApplicationDbContext db, IBuildingScopeService buildingScope)
    {
        _db = db;
        _buildingScope = buildingScope;
    }

    public async Task<RoomDto> Handle(ChangeRoomStatusCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<RoomStatus>(request.Status, true, out var targetStatus))
            throw new BadRequestException($"Invalid status: '{request.Status}'. Must be 'Available' or 'Maintenance'.");

        // SM-05: Only Available and Maintenance are valid targets for manual PATCH
        if (targetStatus is not (RoomStatus.Available or RoomStatus.Maintenance))
            throw new BadRequestException("Manual status change only supports Available or Maintenance.");

        var room = await _db.Rooms
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Room {request.Id} not found.");

        // AUTH-05
        await _buildingScope.AuthorizeAsync(room.BuildingId, cancellationToken);

        // SM-05: Current status must also be Available or Maintenance
        if (room.Status is not (RoomStatus.Available or RoomStatus.Maintenance))
            throw new ConflictException(
                $"Cannot manually change status from {room.Status}. Room must be Available or Maintenance.",
                "INVALID_STATUS_TRANSITION");

        if (room.Status == targetStatus)
            throw new BadRequestException($"Room is already {targetStatus}.");

        room.Status = targetStatus;
        room.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "Room was modified by another user. Please retry.",
                "CONCURRENCY_CONFLICT");
        }

        return new RoomDto
        {
            Id = room.Id,
            BuildingId = room.BuildingId,
            RoomNumber = room.RoomNumber,
            Floor = room.Floor,
            Area = room.Area,
            Price = room.Price,
            MaxOccupants = room.MaxOccupants,
            Description = room.Description,
            Status = room.Status.ToString(),
            Images = room.Images,
            CreatedAt = room.CreatedAt,
            UpdatedAt = room.UpdatedAt
        };
    }
}
