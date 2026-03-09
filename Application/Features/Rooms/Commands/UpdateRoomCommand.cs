using Application.Features.Rooms.DTOs;
using MediatR;

namespace Application.Features.Rooms.Commands;

/// <summary>
/// Updates a room. OWNER or STAFF (if assigned to building).
/// Partial update: only non-null fields are applied.
/// </summary>
public record UpdateRoomCommand : IRequest<RoomDto>
{
    public Guid Id { get; init; }
    public string? RoomNumber { get; init; }
    public int? Floor { get; init; }
    public decimal? Area { get; init; }
    public decimal? Price { get; init; }
    public int? MaxOccupants { get; init; }
    public string? Description { get; init; }
}
