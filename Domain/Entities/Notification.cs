using Domain.Enums;

namespace Domain.Entities;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? SenderId { get; set; }
    public Guid ReceiverId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public NotificationType Type { get; set; } = NotificationType.General;
    public bool IsRead { get; set; } = false;
    public Guid? ReferenceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? Sender { get; set; }
    public User? Receiver { get; set; }
}
