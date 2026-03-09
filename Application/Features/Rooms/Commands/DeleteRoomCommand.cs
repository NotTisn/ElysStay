using MediatR;

namespace Application.Features.Rooms.Commands;

/// <summary>
/// Soft-deletes a room. OWNER only.
/// SD-04: Returns 409 Conflict if the room has an active contract.
/// </summary>
public record DeleteRoomCommand(Guid Id) : IRequest;
