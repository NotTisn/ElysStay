using Domain.Enums;

namespace Domain.Entities;

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? InvoiceId { get; set; }
    public Guid? ContractId { get; set; }
    public PaymentType Type { get; set; }
    public decimal Amount { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
    public string? ReferenceCode { get; set; }
    public string? Note { get; set; }
    public Guid RecordedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Invoice? Invoice { get; set; }
    public Contract? Contract { get; set; }
    public User? Recorder { get; set; }
}