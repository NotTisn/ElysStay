namespace Domain.Entities;

public class StaffAssignment
{
    public Guid StaffId { get; set; }
    public Guid BuildingId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public User? Staff { get; set; }
    public Building? Building { get; set; }
}
