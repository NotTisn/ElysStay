namespace Domain.Entities;

public class StaffAssignment
{
    public Guid BuildingId { get; set; }
    public Guid StaffId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Building? Building { get; set; }
    public User? Staff { get; set; }
}
