namespace Application.Features.Payments.DTOs;

public record PaymentSummaryDto(
    decimal TotalAmount,
    decimal RentPayments,
    decimal DepositsIn,
    decimal DepositsRefunded,
    int PaymentCount);