using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Rooms.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Rooms.Commands;

public class CreateRoomCommandHandler : IRequestHandler<CreateRoomCommand, RoomDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IBuildingScopeService _buildingScope;

    public CreateRoomCommandHandler(IApplicationDbContext db, IBuildingScopeService buildingScope)
    {
        _db = db;
        _buildingScope = buildingScope;
    }

    public async Task<RoomDto> Handle(CreateRoomCommand request, CancellationToken cancellationToken)
    {
        // AUTH-05: Building-scope check
        await _buildingScope.AuthorizeAsync(request.BuildingId, cancellationToken);

        // Verify building exists
        var building = await _db.Buildings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BuildingId, cancellationToken)
            ?? throw new NotFoundException($"Building {request.BuildingId} not found.");

        // VAL-03: floor range
        if (request.Floor < 1 || request.Floor > building.TotalFloors)
            throw new BadRequestException($"Floor must be between 1 and {building.TotalFloors}.");

        // UQ-04: room number unique within building
        var exists = await _db.Rooms
            .AnyAsync(r => r.BuildingId == request.BuildingId && r.RoomNumber == request.RoomNumber, cancellationToken);
        if (exists)
            throw new ConflictException(
                $"Room number '{request.RoomNumber}' already exists in this building.",
                "DUPLICATE_ROOM_NUMBER");

        var room = new Room
        {
            BuildingId = request.BuildingId,
            RoomNumber = request.RoomNumber.Trim(),
            Floor = request.Floor,
            Area = request.Area,
            Price = request.Price,
            MaxOccupants = request.MaxOccupants,
            Description = request.Description?.Trim()
        };

        _db.Rooms.Add(room);
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
