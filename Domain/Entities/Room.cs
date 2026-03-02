using Domain.Enums;

namespace Domain.Entities;

public class Room
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BuildingId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public int Floor { get; set; }
    public decimal Area { get; set; }
    public decimal Price { get; set; }
    public int MaxOccupants { get; set; } = 2;
    public string? Description { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Available;
    public string? Images { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public Building? Building { get; set; }
    public ICollection<RoomService> RoomServices { get; set; } = new List<RoomService>();
    public ICollection<RoomReservation> Reservations { get; set; } = new List<RoomReservation>();
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
    public ICollection<MeterReading> MeterReadings { get; set; } = new List<MeterReading>();
    public ICollection<MaintenanceIssue> Issues { get; set; } = new List<MaintenanceIssue>();
}