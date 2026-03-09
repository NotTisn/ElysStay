namespace Application.Features.Rooms.DTOs;

/// <summary>
/// Room summary DTO for list responses.
/// </summary>
public record RoomDto
{
    public required Guid Id { get; init; }
    public required Guid BuildingId { get; init; }
    public required string RoomNumber { get; init; }
    public required int Floor { get; init; }
    public required decimal Area { get; init; }
    public required decimal Price { get; init; }
    public required int MaxOccupants { get; init; }
    public string? Description { get; init; }
    public required string Status { get; init; }
    public string? Images { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
