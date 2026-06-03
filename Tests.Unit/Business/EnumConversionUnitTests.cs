using Xunit;
using FluentAssertions;
using Domain.Enums;

namespace ElysStay.Tests.Unit.Business;

public class EnumConversionUnitTests
{
    [Fact]
    public void UserRole_CanConvertToString_Correctly()
    {
        // Arrange & Act
        var role = UserRole.Tenant;

        // Assert
        role.ToString().Should().Be("Tenant");
    }

    [Fact]
    public void InvoiceStatus_CanParseFromString()
    {
        // Arrange
        var statusString = "Draft";

        // Act
        var status = Enum.Parse<InvoiceStatus>(statusString);

        // Assert
        status.Should().Be(InvoiceStatus.Draft);
    }

    [Fact]
    public void PaymentType_AllValuesAreValid()
    {
        // Arrange & Act
        var types = new[] { PaymentType.RentPayment, PaymentType.DepositIn, PaymentType.DepositRefund };

        // Assert
        types.Should().AllSatisfy(t => Enum.IsDefined(typeof(PaymentType), t).Should().BeTrue());
    }

    [Fact]
    public void RoomStatus_CanConvertBetweenEnumAndString()
    {
        // Arrange
        var status = RoomStatus.Available;

        // Act
        var statusString = status.ToString();
        var parsedStatus = Enum.Parse<RoomStatus>(statusString);

        // Assert
        parsedStatus.Should().Be(status);
    }
    }
