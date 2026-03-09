namespace Application.Features.Expenses.DTOs;

public record ExpenseDto(
    Guid Id,
    Guid BuildingId,
    string BuildingName,
    Guid? RoomId,
    string? RoomNumber,
    string Category,
    string Description,
    decimal Amount,
    string? ReceiptUrl,
    DateOnly ExpenseDate,
    Guid RecordedBy,
    string? RecorderName,
    DateTime CreatedAt,
    DateTime UpdatedAt);
