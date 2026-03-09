using Application.Features.Rooms.DTOs;
using MediatR;

namespace Application.Features.Rooms.Queries;

/// <summary>
/// Gets a single room by ID with full detail.
/// Subject to building-scope authorization.
/// </summary>
public record GetRoomByIdQuery(Guid Id) : IRequest<RoomDto>;
