namespace Domain.Entities;

public class InvoiceDetail
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId { get; set; }
    public Guid? ServiceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
    public decimal? PreviousReading { get; set; }
    public decimal? CurrentReading { get; set; }

    // Navigation properties
    public Invoice? Invoice { get; set; }
    public Service? Service { get; set; }
}
