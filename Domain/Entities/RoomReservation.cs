using Domain.Enums;

namespace Domain.Entities;

public class RoomReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    public Guid TenantUserId { get; set; }
    public decimal DepositAmount { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);
    public string? Note { get; set; }
    public decimal? RefundAmount { get; set; }
    public DateTime? RefundedAt { get; set; }
    public string? RefundNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Room? Room { get; set; }
    public User? TenantUser { get; set; }
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}
