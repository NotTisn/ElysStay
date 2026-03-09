using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Rooms.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Rooms.Commands;

public class UpdateRoomCommandHandler : IRequestHandler<UpdateRoomCommand, RoomDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IBuildingScopeService _buildingScope;

    public UpdateRoomCommandHandler(IApplicationDbContext db, IBuildingScopeService buildingScope)
    {
        _db = db;
        _buildingScope = buildingScope;
    }

    public async Task<RoomDto> Handle(UpdateRoomCommand request, CancellationToken cancellationToken)
    {
        var room = await _db.Rooms
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Room {request.Id} not found.");

        // AUTH-05: Building-scope check
        await _buildingScope.AuthorizeAsync(room.BuildingId, cancellationToken);

        // Validate floor if changed (VAL-03)
        if (request.Floor.HasValue)
        {
            var building = await _db.Buildings
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == room.BuildingId, cancellationToken)
                ?? throw new NotFoundException($"Building {room.BuildingId} not found.");

            if (request.Floor.Value < 1 || request.Floor.Value > building.TotalFloors)
                throw new BadRequestException($"Floor must be between 1 and {building.TotalFloors}.");
        }

        // Validate uniqueness if room number changed (UQ-04)
        if (request.RoomNumber is not null && request.RoomNumber != room.RoomNumber)
        {
            var exists = await _db.Rooms
                .AnyAsync(r => r.BuildingId == room.BuildingId
                            && r.RoomNumber == request.RoomNumber
                            && r.Id != room.Id, cancellationToken);
            if (exists)
                throw new ConflictException(
                    $"Room number '{request.RoomNumber}' already exists in this building.",
                    "DUPLICATE_ROOM_NUMBER");
        }

        // Partial update
        if (request.RoomNumber is not null)
            room.RoomNumber = request.RoomNumber.Trim();

        if (request.Floor.HasValue)
            room.Floor = request.Floor.Value;

        if (request.Area.HasValue)
            room.Area = request.Area.Value;

        if (request.Price.HasValue)
            room.Price = request.Price.Value;

        if (request.MaxOccupants.HasValue)
            room.MaxOccupants = request.MaxOccupants.Value;

        if (request.Description is not null)
            room.Description = request.Description.Trim();

        room.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

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
