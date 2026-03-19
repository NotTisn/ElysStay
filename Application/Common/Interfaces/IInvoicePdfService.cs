namespace Application.Common.Interfaces;

/// <summary>
/// Generates PDF documents for invoices.
/// </summary>
public interface IInvoicePdfService
{
    /// <summary>
    /// Generates a PDF for an invoice. Returns the PDF bytes.
    /// </summary>
    byte[] Generate(InvoicePdfData data);
}

/// <summary>
/// Data required to render an invoice PDF.
/// </summary>
public class InvoicePdfData
{
    public required string BuildingName { get; init; }
    public required string BuildingAddress { get; init; }
    public required string OwnerName { get; init; }
    public required string RoomNumber { get; init; }
    public required string TenantName { get; init; }
    public required int BillingMonth { get; init; }
    public required int BillingYear { get; init; }
    public required decimal RentAmount { get; init; }
    public required decimal ServiceAmount { get; init; }
    public required decimal PenaltyAmount { get; init; }
    public required decimal DiscountAmount { get; init; }
    public required decimal TotalAmount { get; init; }
    public required decimal PaidAmount { get; init; }
    public required DateOnly DueDate { get; init; }
    public required string Status { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required IReadOnlyList<InvoiceDetailLine> Details { get; init; }
    public string? Note { get; init; }
}

public class InvoiceDetailLine
{
    public required string ServiceName { get; init; }
    public required string Unit { get; init; }
    public decimal? OldReading { get; init; }
    public decimal? NewReading { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public required decimal Amount { get; init; }
}
