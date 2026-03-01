using Domain.Enums;

namespace Domain.Entities;

public class Expense
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BuildingId { get; set; }
    public Guid? RoomId { get; set; }
    public Guid RecordedById { get; set; }
    public ExpenseCategory Category { get; set; } = ExpenseCategory.Other;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime ExpenseDate { get; set; } = DateTime.UtcNow.Date;
    public string? ReceiptImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public Building? Building { get; set; }
    public Room? Room { get; set; }
    public User? RecordedBy { get; set; }
}
