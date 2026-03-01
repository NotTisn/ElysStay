namespace Domain.Entities;

public class Building
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? AddressNumber { get; set; }
    public string? AddressStreet { get; set; }
    public string? AddressWard { get; set; }
    public string? AddressDistrict { get; set; }
    public string? AddressCity { get; set; }
    public string? Description { get; set; }
    public string Images { get; set; } = "[]"; 
    public string SharedAmenities { get; set; } = "[]";
    public int TotalRooms { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public ICollection<Room> Rooms { get; set; } = new List<Room>();
    public ICollection<StaffAssignment> StaffAssignments { get; set; } = new List<StaffAssignment>();
}
