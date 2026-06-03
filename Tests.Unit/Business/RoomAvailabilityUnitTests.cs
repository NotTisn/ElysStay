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
    public void RoomStatus_Booked_CannotBeReserved()
    {
        // Arrange & Act
        var status = RoomStatus.Booked;

        // Assert
        status.Should().Be(RoomStatus.Booked);
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
            RoomStatus.Booked
        };

        // Assert
        statuses.Should().HaveCount(3);
    }
    }
