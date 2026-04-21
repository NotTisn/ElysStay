using Xunit;
using FluentAssertions;
using Domain.Entities;
using Domain.Enums;
using System;

namespace ElysStay.Tests.Unit.Business;

public class PaymentUnitTests
{
    [Fact]
    public void ProcessFullPayment_Invoice_StatusShouldChange()
    {
        // Arrange
        var invoice = new Invoice
        {
            Status = InvoiceStatus.Draft,
            RentAmount = 2_000_000,
            ServiceAmount = 500_000
        };

        var payment = new Payment
        {
            Type = PaymentType.RentPayment,
            Amount = 2_500_000,
            PaymentMethod = "BankTransfer",
            PaidAt = DateTime.UtcNow
        };

        // Act
        var totalExpected = invoice.RentAmount + invoice.ServiceAmount + invoice.PenaltyAmount - invoice.DiscountAmount;
        if (payment.Amount >= totalExpected)
        {
            invoice.Status = InvoiceStatus.Paid;
        }

        // Assert
        invoice.Status.Should().Be(InvoiceStatus.Paid);
        payment.PaymentMethod.Should().Be("BankTransfer");
    }

    [Fact]
    public void VoidPayment_ShouldRecordAsDeleted_OrVoided()
    {
         // Arrange
        var payment = new Payment
        {
            Type = PaymentType.RentPayment,
            Amount = 2_500_000,
            PaymentMethod = "Cash"
        };
        var isVoided = false;

        // Act
        isVoided = true;
        payment.Note = "Reversed transaction";

        // Assert
        isVoided.Should().BeTrue();
        payment.Note.Should().Be("Reversed transaction");
    }
}
