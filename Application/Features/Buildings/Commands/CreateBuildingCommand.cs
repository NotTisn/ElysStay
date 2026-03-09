using Application.Features.Buildings.DTOs;
using MediatR;

namespace Application.Features.Buildings.Commands;

/// <summary>
/// Creates a building. OWNER only.
/// Automatically creates 5 default services (BD-01).
/// </summary>
public record CreateBuildingCommand : IRequest<BuildingDto>
{
    public required string Name { get; init; }
    public required string Address { get; init; }
    public string? Description { get; init; }
    public required int TotalFloors { get; init; }
    public int InvoiceDueDay { get; init; } = 10;
}
