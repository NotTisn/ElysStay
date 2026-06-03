using Xunit;
using FluentAssertions;
using Domain.Entities;
using Domain.Enums;
using System;

namespace ElysStay.Tests.Unit.Business;

public class PropertyRoomUnitTests
{
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
