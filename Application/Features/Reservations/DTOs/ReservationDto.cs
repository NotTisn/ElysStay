namespace Application.Features.Reservations.DTOs;

public record ReservationDto(
    Guid Id,
    Guid RoomId,
    string RoomNumber,
    Guid BuildingId,
    string BuildingName,
    Guid TenantUserId,
    string? TenantName,
    decimal DepositAmount,
    string Status,
    DateTime ExpiresAt,
    string? Note,
    decimal? RefundAmount,
    DateTime? RefundedAt,
    string? RefundNote,
    DateTime CreatedAt,
    DateTime UpdatedAt);
