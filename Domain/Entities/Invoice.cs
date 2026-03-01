using Domain.Enums;

namespace Domain.Entities;

public class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string InvoiceCode { get; set; } = string.Empty;
    public Guid BuildingId { get; set; }
    public Guid ContractId { get; set; }
    public Guid TenantId { get; set; }
    public int BillingMonth { get; set; }
    public int BillingYear { get; set; }
    public decimal RoomCharge { get; set; }
    public decimal ElectricityCharge { get; set; }
    public int ElectricityUsage { get; set; }
    public decimal WaterCharge { get; set; }
    public int WaterUsage { get; set; }
    public string ServiceFees { get; set; } = "[]"; 
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; } = 0;
    public decimal RemainingAmount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Unpaid;
    public DateTime DueDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public Building? Building { get; set; }
    public Contract? Contract { get; set; }
    public User? Tenant { get; set; }
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
