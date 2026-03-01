using Domain.Enums;

namespace Domain.Entities;

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId { get; set; }
    public Guid? PayerId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? TransferDetails { get; set; } 
    public Guid ReceiverId { get; set; }
    public string? ReceiptImageUrl { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    public Invoice? Invoice { get; set; }
    public User? Payer { get; set; }
    public User? Receiver { get; set; }
}
