using Xunit;
using FluentAssertions;
using Domain.Entities;
using System;

namespace ElysStay.Tests.Unit.Business;

public class ServiceManagementUnitTests
{
    [Fact]
    public void NewService_ShouldBeActive_ByDefault()
    {
        // Arrange
        var service = new Service
        {
            Name = "Internet",
            UnitPrice = 150_000,
            IsMetered = false
        };

        // Assert
        service.IsActive.Should().BeTrue();
    }

    [Fact]
    public void UpdateServicePrice_ShouldTrackPreviousPrice_And_UpdatedAtDate()
    {
        // Arrange
        var service = new Service
        {
            Name = "Water",
            UnitPrice = 15_000,
            IsMetered = true
        };

        // Act
        service.PreviousUnitPrice = service.UnitPrice;
        service.UnitPrice = 20_000;
        service.PriceUpdatedAt = DateTime.UtcNow;

        // Assert
        service.UnitPrice.Should().Be(20_000);
        service.PreviousUnitPrice.Should().Be(15_000);
        service.PriceUpdatedAt.Should().NotBeNull();
        service.PriceUpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void RoomService_OverridePrice_CalculatesCorrectly()
    {
        // Arrange
        var baseService = new Service
        {
            Name = "Internet",
            UnitPrice = 150_000
        };

        var roomService = new RoomService
        {
            Service = baseService,
            OverrideUnitPrice = 100_000
        };

        // Act
        decimal effectivePrice = roomService.OverrideUnitPrice ?? baseService.UnitPrice;

        // Assert
        effectivePrice.Should().Be(100_000);
    }
}
