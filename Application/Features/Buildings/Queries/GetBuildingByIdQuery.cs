using Application.Features.Buildings.DTOs;
using MediatR;

namespace Application.Features.Buildings.Queries;

/// <summary>
/// Gets a single building by ID with room stats (totalRooms, occupancyRate).
/// Subject to building-scope authorization.
/// </summary>
public record GetBuildingByIdQuery(Guid Id) : IRequest<BuildingDetailDto>;
