namespace Domain.Entities;

public class RoomService
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    public Guid ServiceId { get; set; }
    public bool IsEnabled { get; set; } = true;
    public decimal? OverrideUnitPrice { get; set; }
    public decimal? OverrideQuantity { get; set; }

    // Navigation properties
    public Room? Room { get; set; }
    public Service? Service { get; set; }
}
