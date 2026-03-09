namespace Application.Features.Buildings.DTOs;

/// <summary>
/// Building summary DTO for list responses.
/// </summary>
public record BuildingDto
{
    public required Guid Id { get; init; }
    public required Guid OwnerId { get; init; }
    public required string Name { get; init; }
    public required string Address { get; init; }
    public string? Description { get; init; }
    public required int TotalFloors { get; init; }
    public required int InvoiceDueDay { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Building detail with room stats.
/// Returned by GET /buildings/{id}.
/// </summary>
public record BuildingDetailDto : BuildingDto
{
    public required int TotalRooms { get; init; }
    public required double OccupancyRate { get; init; }
}
