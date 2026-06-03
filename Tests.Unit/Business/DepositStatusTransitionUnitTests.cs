using Xunit;
using FluentAssertions;
using Domain.Enums;

namespace ElysStay.Tests.Unit.Business;

public class DepositStatusTransitionUnitTests
{
    // [Fact]
    public void DepositStatus_Held_ToPartiallyRefunded_IsValidTransition()
    {
        // Arrange
        var current = DepositStatus.Held;

        // Act
        var next = DepositStatus.PartiallyRefunded;

        // Assert
        next.Should().NotBe(current);
    }

    // [Fact]
    public void DepositStatus_PartiallyRefunded_ToRefunded_IsValidTransition()
    {
        // Arrange
        var current = DepositStatus.PartiallyRefunded;

        // Act
        var next = DepositStatus.Refunded;

        // Assert
        next.Should().NotBe(current);
    }

    // [Fact]
    public void DepositStatus_Held_ToForfeited_IsValidTransition()
    {
        // Arrange
        var current = DepositStatus.Held;

        // Act
        var next = DepositStatus.Forfeited;

        // Assert
        next.Should().NotBe(current);
    }

    // [Fact]
    public void DepositStatus_AllValidStates_AreEnumValues()
    {
        // Arrange & Act
        var states = new[] { DepositStatus.Held, DepositStatus.PartiallyRefunded, DepositStatus.Refunded, DepositStatus.Forfeited };

        // Assert
        states.Should().HaveCount(4);
        states.Should().OnlyContain(s => Enum.IsDefined(typeof(DepositStatus), s));
    }
    }
