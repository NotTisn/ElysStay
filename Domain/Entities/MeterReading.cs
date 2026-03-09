namespace Domain.Entities;

public class MeterReading
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    public Guid ServiceId { get; set; }
    public int BillingYear { get; set; }
    public int BillingMonth { get; set; }
    public decimal PreviousReading { get; set; }
    public decimal CurrentReading { get; set; }
    public decimal Consumption { get; set; }
    public DateTime DateRead { get; set; } = DateTime.UtcNow;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Room? Room { get; set; }
    public Service? Service { get; set; }
    public User? Creator { get; set; }
}