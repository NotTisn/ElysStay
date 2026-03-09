namespace Application.Features.MaintenanceIssues.DTOs;

public record MaintenanceIssueDto(
    Guid Id,
    Guid BuildingId,
    string BuildingName,
    Guid? RoomId,
    string? RoomNumber,
    Guid ReportedBy,
    string? ReporterName,
    Guid? AssignedTo,
    string? AssigneeName,
    string Title,
    string Description,
    string[]? ImageUrls,
    string Status,
    string Priority,
    DateTime CreatedAt,
    DateTime UpdatedAt);
