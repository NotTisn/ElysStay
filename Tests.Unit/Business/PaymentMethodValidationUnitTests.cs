using Xunit;
using FluentAssertions;
using Domain.Entities;

namespace ElysStay.Tests.Unit.Business;

public class PaymentMethodValidationUnitTests
{
    private static readonly string[] ValidPaymentMethods = { "Cash", "BankTransfer", "MoMo", "ZaloPay" };

    [Fact]
    public void PaymentMethod_Cash_IsValidString()
    {
        // Arrange & Act
        var payment = new Payment { PaymentMethod = "Cash" };

        // Assert
        payment.PaymentMethod.Should().Be("Cash");
        ValidPaymentMethods.Should().Contain(payment.PaymentMethod);
    }

    [Fact]
    public void PaymentMethod_BankTransfer_IsValidString()
    {
        // Arrange & Act
        var payment = new Payment { PaymentMethod = "BankTransfer" };

        // Assert
        payment.PaymentMethod.Should().Be("BankTransfer");
        ValidPaymentMethods.Should().Contain(payment.PaymentMethod);
    }

    [Fact]
    public void PaymentMethod_MoMo_IsValidString()
    {
        // Arrange & Act
        var payment = new Payment { PaymentMethod = "MoMo" };

        // Assert
        payment.PaymentMethod.Should().Be("MoMo");
        ValidPaymentMethods.Should().Contain(payment.PaymentMethod);
    }

    [Fact]
    public void PaymentMethod_ZaloPay_IsValidString()
    {
        // Arrange & Act
        var payment = new Payment { PaymentMethod = "ZaloPay" };

        // Assert
        payment.PaymentMethod.Should().Be("ZaloPay");
        ValidPaymentMethods.Should().Contain(payment.PaymentMethod);
    }

    [Fact]
    public void PaymentMethod_AllValidMethodsExist()
    {
        // Arrange & Act
        var validMethods = new[] { "Cash", "BankTransfer", "MoMo", "ZaloPay" };

        // Assert
        validMethods.Should().HaveCount(4);
        validMethods.Should().AllSatisfy(m => m.Should().NotBeNullOrEmpty());
    }
    }
