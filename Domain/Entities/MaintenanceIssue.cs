using Domain.Enums;

namespace Domain.Entities;

public class MaintenanceIssue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BuildingId { get; set; }
    public Guid RoomId { get; set; }
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IssueType { get; set; }
    public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;
    public IssueStatus Status { get; set; } = IssueStatus.New;
    public string Images { get; set; } = "[]"; 
    public Guid? HandledById { get; set; }
    public string? ResolutionNotes { get; set; }
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    public DateTime? HandledAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public Building? Building { get; set; }
    public Room? Room { get; set; }
    public User? Tenant { get; set; }
    public User? HandledBy { get; set; }
}
