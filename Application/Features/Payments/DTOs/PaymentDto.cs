namespace Application.Features.Payments.DTOs;

/// <summary>
/// Payment DTO.
/// </summary>
public record PaymentDto
{
    public required Guid Id { get; init; }
    public Guid? InvoiceId { get; init; }
    public Guid? ContractId { get; init; }
    public required string Type { get; init; }
    public required decimal Amount { get; init; }
    public string? PaymentMethod { get; init; }
    public string? Note { get; init; }
    public required DateTime PaidAt { get; init; }
    public required Guid RecordedBy { get; init; }
    public string? RecorderName { get; init; }
    public required DateTime CreatedAt { get; init; }
}
