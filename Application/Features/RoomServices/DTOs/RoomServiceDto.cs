namespace Application.Features.RoomServices.DTOs;

public record RoomServiceDto
{
    public required Guid ServiceId { get; init; }
    public required string ServiceName { get; init; }
    public required string Unit { get; init; }
    public required decimal BuildingUnitPrice { get; init; }
    public required bool IsMetered { get; init; }
    public required bool IsEnabled { get; init; }
    public decimal? OverrideUnitPrice { get; init; }
    public decimal? OverrideQuantity { get; init; }
    public required decimal EffectiveUnitPrice { get; init; }
}
