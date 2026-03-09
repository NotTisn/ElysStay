using Application.Features.Buildings.DTOs;
using MediatR;

namespace Application.Features.Buildings.Commands;

/// <summary>
/// Updates a building. OWNER only.
/// Partial update: only non-null fields are applied.
/// </summary>
public record UpdateBuildingCommand : IRequest<BuildingDto>
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public string? Address { get; init; }
    public string? Description { get; init; }
    public int? TotalFloors { get; init; }
    public int? InvoiceDueDay { get; init; }
}
