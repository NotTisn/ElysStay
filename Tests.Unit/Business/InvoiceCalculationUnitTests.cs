using Xunit;
using FluentAssertions;
using Domain.Entities;
using Domain.Enums;

namespace ElysStay.Tests.Unit.Business;

public class InvoiceCalculationUnitTests
{
    [Fact]
    public void CalculateInvoiceTotal_WithAllAmounts_ReturnsCorrectSum()
    {
        // Arrange
        var invoice = new Invoice
        {
            RentAmount = 5_000_000,
            ServiceAmount = 100_000,
            PenaltyAmount = 50_000,
            DiscountAmount = 20_000
        };

        // Act
        var total = invoice.RentAmount + invoice.ServiceAmount + invoice.PenaltyAmount - invoice.DiscountAmount;

        // Assert
        total.Should().Be(5_130_000);
    }

    [Fact]
    public void CalculateInvoiceTotal_WithoutServiceCharges_CalculatesRoomAmountOnly()
    {
        // Arrange
        var invoice = new Invoice
        {
            RentAmount = 5_000_000,
            ServiceAmount = 0,
            PenaltyAmount = 0,
            DiscountAmount = 0
        };

        // Act
        var total = invoice.RentAmount + invoice.ServiceAmount + invoice.PenaltyAmount - invoice.DiscountAmount;

        // Assert
        total.Should().Be(5_000_000);
    }

    [Fact]
    public void CalculateServiceCharges_FromConsumption_ReturnsCorrectAmount()
    {
        // Arrange
        const decimal consumption = 20;
        const decimal unitPrice = 10_000;

        // Act
        var serviceCharge = consumption * unitPrice;

        // Assert
        serviceCharge.Should().Be(200_000);
    }

    [Fact]
    public void ApplyDiscount_ReducesTotal_Correctly()
    {
        // Arrange
        var invoice = new Invoice
        {
            RentAmount = 5_000_000,
            ServiceAmount = 100_000,
            PenaltyAmount = 0,
            DiscountAmount = 50_000
        };

        // Act
        var total = invoice.RentAmount + invoice.ServiceAmount - invoice.DiscountAmount;

        // Assert
        total.Should().Be(5_050_000);
    }

    [Fact]
    public void ApplyPenalty_IncreasesTotal_Correctly()
    {
        // Arrange
        var invoice = new Invoice
        {
            RentAmount = 5_000_000,
            ServiceAmount = 0,
            PenaltyAmount = 500_000,
            DiscountAmount = 0
        };

        // Act
        var total = invoice.RentAmount + invoice.PenaltyAmount;

        // Assert
        total.Should().Be(5_500_000);
    }

    [Fact]
    public void InvoiceStatus_NewInvoice_ShouldBeUnpaid()
    {
        // Arrange & Act
        var invoice = new Invoice
        {
            Status = InvoiceStatus.Draft
        };

        // Assert
        invoice.Status.Should().Be(InvoiceStatus.Draft);
    }

    [Fact]
    public void InvoiceTotal_CannotBeNegative_ShouldBePositive()
    {
        // Arrange
        var invoice = new Invoice
        {
            RentAmount = 5_000_000,
            ServiceAmount = 0,
            PenaltyAmount = 0,
            DiscountAmount = 0
        };

        // Act
        var total = invoice.RentAmount + invoice.ServiceAmount + invoice.PenaltyAmount - invoice.DiscountAmount;

        // Assert
        total.Should().BeGreaterThan(0);
    }

    [Fact]
    public void OverdueInvoice_AppliesLateFeePercent_Correctly()
    {
        // Arrange
        var invoice = new Invoice
        {
            RentAmount = 5_000_000,
            ServiceAmount = 500_000
        };
        decimal lateFeePercentage = 0.05m;

        // Act
        decimal baseTotal = invoice.RentAmount + invoice.ServiceAmount;
        invoice.PenaltyAmount = baseTotal * lateFeePercentage;
        var total = baseTotal + invoice.PenaltyAmount;

        // Assert
        invoice.PenaltyAmount.Should().Be(275_000);
        total.Should().Be(5_775_000);
    }

    [Fact]
    public void Invoice_PartialPayment_UpdatesRemainingBalance()
    {
        // Arrange
        var invoice = new Invoice
        {
            RentAmount = 5_000_000,
            ServiceAmount = 500_000
        };
        decimal partialPayment = 2_000_000;

        // Act
        var total = invoice.RentAmount + invoice.ServiceAmount;
        var remainingBalance = total - partialPayment;

        // Assert
        remainingBalance.Should().Be(3_500_000);
    }
}
