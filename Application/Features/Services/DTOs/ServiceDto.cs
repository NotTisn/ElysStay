namespace Application.Features.Services.DTOs;

public record ServiceDto
{
    public required Guid Id { get; init; }
    public required Guid BuildingId { get; init; }
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required decimal UnitPrice { get; init; }
    public decimal? PreviousUnitPrice { get; init; }
    public DateTime? PriceUpdatedAt { get; init; }
    public required bool IsMetered { get; init; }
    public required bool IsActive { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
