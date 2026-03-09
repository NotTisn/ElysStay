using Application.Common.Models;
using Application.Features.Rooms.DTOs;
using MediatR;

namespace Application.Features.Rooms.Queries;

/// <summary>
/// Lists rooms across all buildings (or filtered by buildingId).
/// Owner sees all their rooms. Staff sees only rooms in assigned buildings.
/// </summary>
public record GetRoomsQuery : IRequest<PagedResult<RoomDto>>
{
    public Guid? BuildingId { get; init; }
    public string? Status { get; init; }
    public int? Floor { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string Sort { get; init; } = "createdAt:desc";
}
