using Application.Features.Rooms.DTOs;
using MediatR;

namespace Application.Features.Rooms.Commands;

/// <summary>
/// Manual room status change. OWNER/STAFF only.
/// SM-05: Only AVAILABLE ↔ MAINTENANCE allowed via this endpoint.
/// </summary>
public record ChangeRoomStatusCommand : IRequest<RoomDto>
{
    public Guid Id { get; init; }
    public required string Status { get; init; }
}
