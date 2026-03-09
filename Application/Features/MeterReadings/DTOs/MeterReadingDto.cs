namespace Application.Features.MeterReadings.DTOs;

/// <summary>
/// Meter reading DTO.
/// </summary>
public record MeterReadingDto
{
    public required Guid Id { get; init; }
    public required Guid RoomId { get; init; }
    public required string RoomNumber { get; init; }
    public required Guid ServiceId { get; init; }
    public required string ServiceName { get; init; }
    public required string ServiceUnit { get; init; }
    public required int BillingYear { get; init; }
    public required int BillingMonth { get; init; }
    public required decimal PreviousReading { get; init; }
    public required decimal CurrentReading { get; init; }
    public required decimal Consumption { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
