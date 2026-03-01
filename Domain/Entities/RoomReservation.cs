using Domain.Enums;

namespace Domain.Entities;

public class RoomReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BuildingId { get; set; }
    public Guid RoomId { get; set; }
    public string GuestName { get; set; } = string.Empty;
    public string GuestPhone { get; set; } = string.Empty;
    public decimal ReservationFee { get; set; }
    public DateTime ExpectedMoveInDate { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Building? Building { get; set; }
    public Room? Room { get; set; }
}
