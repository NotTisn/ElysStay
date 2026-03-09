namespace Domain.Entities;

public class Expense
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BuildingId { get; set; }
    public Guid? RoomId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? ReceiptUrl { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public Guid RecordedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Building? Building { get; set; }
    public Room? Room { get; set; }
    public User? Recorder { get; set; }
}
