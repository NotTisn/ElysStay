namespace Application.Features.Invoices.DTOs;

/// <summary>
/// Invoice summary DTO for list responses.
/// PAY-01: PaidAmount is computed from SUM(Payment.Amount), not stored.
/// </summary>
public record InvoiceDto
{
    public required Guid Id { get; init; }
    public required Guid ContractId { get; init; }
    public required Guid RoomId { get; init; }
    public required string RoomNumber { get; init; }
    public required Guid BuildingId { get; init; }
    public required string BuildingName { get; init; }
    public required Guid TenantUserId { get; init; }
    public required string TenantName { get; init; }
    public required int BillingYear { get; init; }
    public required int BillingMonth { get; init; }
    public required decimal RentAmount { get; init; }
    public required decimal ServiceAmount { get; init; }
    public required decimal PenaltyAmount { get; init; }
    public required decimal DiscountAmount { get; init; }
    public required decimal TotalAmount { get; init; }
    public required decimal PaidAmount { get; init; }
    public required string Status { get; init; }
    public required DateOnly DueDate { get; init; }
    public string? Note { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Invoice detail DTO with line items and payments.
/// </summary>
public record InvoiceDetailDto : InvoiceDto
{
    public required IReadOnlyList<InvoiceLineItemDto> LineItems { get; init; }
}

/// <summary>
/// Line item (InvoiceDetail) DTO.
/// </summary>
public record InvoiceLineItemDto
{
    public required Guid Id { get; init; }
    public Guid? ServiceId { get; init; }
    public required string Description { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public required decimal Amount { get; init; }
    public decimal? PreviousReading { get; init; }
    public decimal? CurrentReading { get; init; }
}

/// <summary>
/// Result of invoice generation.
/// </summary>
public record InvoiceGenerationResult
{
    public required IReadOnlyList<InvoiceDto> Generated { get; init; }
    public required IReadOnlyList<SkippedContract> Skipped { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
}

public record SkippedContract
{
    public required Guid ContractId { get; init; }
    public required string Reason { get; init; }
}
