using Domain.Enums;

namespace Domain.Entities;

public class MaintenanceIssue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BuildingId { get; set; }
    public Guid RoomId { get; set; }
    public Guid ReportedBy { get; set; }
    public Guid? AssignedTo { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public IssueStatus Status { get; set; } = IssueStatus.Pending;
    public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Building? Building { get; set; }
    public Room? Room { get; set; }
    public User? Reporter { get; set; }
    public User? Assignee { get; set; }
}