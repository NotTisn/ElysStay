using Application.Features.Rooms.DTOs;
using MediatR;

namespace Application.Features.Rooms.Commands;

/// <summary>
/// Creates a room in a specific building.
/// VAL-03: Floor must be between 1 and Building.TotalFloors.
/// UQ-04: RoomNumber must be unique within building.
/// </summary>
public record CreateRoomCommand : IRequest<RoomDto>
{
    public Guid BuildingId { get; init; }
    public required string RoomNumber { get; init; }
    public required int Floor { get; init; }
    public required decimal Area { get; init; }
    public required decimal Price { get; init; }
    public int MaxOccupants { get; init; } = 2;
    public string? Description { get; init; }
}
