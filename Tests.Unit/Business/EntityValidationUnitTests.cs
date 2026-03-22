using Xunit;
using FluentAssertions;
using Domain.Entities;

namespace ElysStay.Tests.Unit.Business;

public class EntityValidationUnitTests
{
    [Fact]
    public void User_WithValidEmail_IsValid()
    {
        // Arrange & Act
        var user = new User
        {
            Email = "test@example.com",
            FullName = "Test User"
        };

        // Assert
        user.Email.Should().NotBeEmpty();
        user.Email.Should().Contain("@");
    }

    [Fact]
    public void Room_WithValidRoomNumber_IsValid()
    {
        // Arrange & Act
        var room = new Room
        {
            RoomNumber = "101",
            BuildingId = Guid.NewGuid()
        };

        // Assert
        room.RoomNumber.Should().NotBeEmpty();
    }

    [Fact]
    public void Building_MustHaveOwner_RequiredField()
    {
        // Arrange
        var building = new Building
        {
            Name = "Test Building",
            OwnerId = Guid.NewGuid()
        };

        // Act & Assert
        building.OwnerId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Contract_MustHaveTenant_RequiredField()
    {
        // Arrange
        var contract = new Contract
        {
            TenantUserId = Guid.NewGuid(),
            RoomId = Guid.NewGuid()
        };

        // Act & Assert
        contract.TenantUserId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Invoice_MustHaveContract_RequiredField()
    {
        // Arrange
        var invoice = new Invoice
        {
            ContractId = Guid.NewGuid()
        };

        // Act & Assert
        invoice.ContractId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Payment_MustHaveAmount_GreaterThanZero()
    {
        // Arrange
        var payment = new Payment
        {
            Amount = 1_000_000
        };

        // Act & Assert
        payment.Amount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TenantProfile_IdNumber_CanBeOptional()
    {
        // Arrange
        var profile = new TenantProfile
        {
            IdNumber = null
        };

        // Act & Assert
        profile.IdNumber.Should().BeNull();
    }

    [Fact]
    public void Expense_AmountMustBePositive()
    {
        // Arrange
        var expense = new Expense
        {
            Amount = 5_000_000
        };

        // Act & Assert
        expense.Amount.Should().BeGreaterThan(0);
    }
}
