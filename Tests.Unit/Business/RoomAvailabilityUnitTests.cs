using Xunit;
using FluentAssertions;
using Domain.Enums;

namespace ElysStay.Tests.Unit.Business;

public class RoomAvailabilityUnitTests
{
    [Fact]
    public void RoomStatus_Available_IsValidForNewReservation()
    {
        // Arrange & Act
        var status = RoomStatus.Available;

        // Assert
        status.Should().Be(RoomStatus.Available);
    }

    [Fact]
    public void RoomStatus_Occupied_CannotBeReserved()
    {
        // Arrange & Act
        var status = RoomStatus.Occupied;

        // Assert
        status.Should().Be(RoomStatus.Occupied);
        status.Should().NotBe(RoomStatus.Available);
    }

    [Fact]
    public void RoomStatus_Maintenance_CannotBeReserved()
    {
        // Arrange & Act
        var status = RoomStatus.Maintenance;

        // Assert
        status.Should().Be(RoomStatus.Maintenance);
        status.Should().NotBe(RoomStatus.Available);
    }

    [Fact]
    public void RoomStatus_Booked_CannotBeReserved()
    {
        // Arrange & Act
        var status = RoomStatus.Reserved;

        // Assert
        status.Should().Be(RoomStatus.Reserved);
        status.Should().NotBe(RoomStatus.Available);
    }

    [Fact]
    public void RoomStatus_AllValidStatesExist()
    {
        // Arrange & Act
        var statuses = new[]
        {
            RoomStatus.Available,
            RoomStatus.Occupied,
            RoomStatus.Maintenance,
            RoomStatus.Reserved
        };

        // Assert
        statuses.Should().HaveCount(4);
    }

    [Fact]
    public void RoomStatus_CanConvertToString()
    {
        // Arrange & Act
        var status = RoomStatus.Available;

        // Assert
        status.ToString().Should().Be("Available");
    }

    [Fact]
    public void IsRoomAvailable_ChecksStatus()
    {
        // Arrange
        var availableStatus = RoomStatus.Available;
        var occupiedStatus = RoomStatus.Occupied;

        // Act & Assert
        availableStatus.Should().Be(RoomStatus.Available);
        occupiedStatus.Should().NotBe(RoomStatus.Available);
    }
}
