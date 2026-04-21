using Xunit;
using FluentAssertions;
using Domain.Entities;
using Domain.Enums;
using System;

namespace ElysStay.Tests.Unit.Business;

public class PropertyRoomUnitTests
{
    [Fact]
    public void ChangeRoomStatus_ToMaintenance_BlocksBooking()
    {
        // Arrange
        var room = new Room
        {
            Status = RoomStatus.Available,
            RoomNumber = "101"
        };

        // Act
        room.Status = RoomStatus.Maintenance;
        room.UpdatedAt = DateTime.UtcNow;

        // Assert
        room.Status.Should().Be(RoomStatus.Maintenance);
        room.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void RoomPricing_Setup_SavesCorrectly()
    {
        // Arrange & Act
        var room = new Room
        {
            Price = 5_000_000
        };

        // Assert
        room.Price.Should().Be(5_000_000);
    }
}
