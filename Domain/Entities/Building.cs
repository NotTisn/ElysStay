namespace Domain.Entities;

public class Building
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int TotalFloors { get; set; }
    public int InvoiceDueDay { get; set; } = 10;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public User? Owner { get; set; }
    public ICollection<Room> Rooms { get; set; } = new List<Room>();
    public ICollection<StaffAssignment> BuildingStaffs { get; set; } = new List<StaffAssignment>();
    public ICollection<Service> Services { get; set; } = new List<Service>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}