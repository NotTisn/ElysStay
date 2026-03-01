using Domain.Enums;

namespace Domain.Entities;

public class Room
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BuildingId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public int FloorNumber { get; set; }
    public decimal Area { get; set; }
    public decimal BaseRent { get; set; }
    public decimal DepositAmount { get; set; }
    public int MaxOccupants { get; set; } = 2;
    public RoomStatus Status { get; set; } = RoomStatus.Available;
    public string Amenities { get; set; } = "[]"; 
    public string Images { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public Building? Building { get; set; }
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}
