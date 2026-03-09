using Domain.Enums;

namespace Domain.Entities;

public class Contract
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    public Guid TenantUserId { get; set; }
    public Guid? ReservationId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateOnly MoveInDate { get; set; }
    public decimal MonthlyRent { get; set; }
    public decimal DepositAmount { get; set; }
    public DepositStatus DepositStatus { get; set; } = DepositStatus.Held;
    public ContractStatus Status { get; set; } = ContractStatus.Active;
    public DateOnly? TerminationDate { get; set; }
    public string? TerminationNote { get; set; }
    public decimal? RefundAmount { get; set; }
    public string? Note { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Room? Room { get; set; }
    public User? TenantUser { get; set; }
    public RoomReservation? Reservation { get; set; }
    public User? Creator { get; set; }
    public ICollection<ContractTenant> ContractTenants { get; set; } = new List<ContractTenant>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}