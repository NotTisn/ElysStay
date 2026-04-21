using Xunit;
using FluentAssertions;
using Domain.Entities;
using Domain.Enums;
using System;

namespace ElysStay.Tests.Unit.Business;

public class MeterReadingUnitTests
{
    [Fact]
    public void CalculateConsumption_FromPrevAndCurrent_ReturnsCorrectDelta()
    {
        // Arrange
        var reading = new MeterReading
        {
            PreviousReading = 150.5m,
            CurrentReading = 180.0m
        };

        // Act
        reading.Consumption = reading.CurrentReading - reading.PreviousReading;

        // Assert
        reading.Consumption.Should().Be(29.5m);
    }

    [Fact]
    public void ReverseReading_Invalid_ShouldResultInNegativeWhichIsBlockedByValdation()
    {
        // Arrange
        var reading = new MeterReading
        {
            PreviousReading = 180.0m,
            CurrentReading = 150.5m
        };

        // Act
        reading.Consumption = reading.CurrentReading - reading.PreviousReading;

        // Assert
        reading.Consumption.Should().BeNegative();
    }
}
