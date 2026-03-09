using Application.Common.Models;
using Application.Features.Buildings.DTOs;
using MediatR;

namespace Application.Features.Buildings.Queries;

/// <summary>
/// Lists buildings with optional name/address filter and pagination.
/// Owner sees all their buildings. Staff sees only assigned buildings.
/// </summary>
public record GetBuildingsQuery : IRequest<PagedResult<BuildingDto>>
{
    public string? Name { get; init; }
    public string? Address { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string Sort { get; init; } = "createdAt:desc";
}
