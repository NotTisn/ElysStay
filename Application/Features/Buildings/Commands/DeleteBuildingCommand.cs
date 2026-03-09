using MediatR;

namespace Application.Features.Buildings.Commands;

/// <summary>
/// Soft-deletes a building. OWNER only.
/// SD-04: Returns 409 Conflict if any room has an active contract.
/// </summary>
public record DeleteBuildingCommand(Guid Id) : IRequest;
