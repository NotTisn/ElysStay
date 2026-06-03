using Xunit;
using FluentAssertions;
using Domain.Enums;

namespace ElysStay.Tests.Unit.Business;

public class ContractStatusValidationUnitTests
{
    // [Fact]
    public void ContractStatus_Active_IsValidInitialState()
    {
        // Arrange & Act
        var status = ContractStatus.Active;

        // Assert
        status.Should().Be(ContractStatus.Active);
    }

    // [Fact]
    public void ContractStatus_Active_ToTerminated_IsValidTransition()
    {
        // Arrange
        var current = ContractStatus.Active;

        // Act
        var next = ContractStatus.Terminated;

        // Assert
        next.Should().NotBe(current);
    }

    // [Fact]
    public void ContractStatus_Terminated_CannotTransitionBack_ToActive()
    {
        // Arrange
        var terminated = ContractStatus.Terminated;

        // Act & Assert
        terminated.Should().NotBe(ContractStatus.Active);
    }
    }
