namespace Application.Features.Notifications.DTOs;

public record NotificationDto(
    Guid Id,
    Guid UserId,
    string Title,
    string Message,
    bool IsRead,
    string Type,
    Guid? ReferenceId,
    DateTime CreatedAt);
