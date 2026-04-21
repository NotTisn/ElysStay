using Application.Features.Services.Commands;
using Domain.Entities;
using Domain.Enums;
using ElysStay.Tests.Integration.TestDoubles;
using FluentAssertions;
using Tests.Integration.Builders;
using Tests.Integration.Fixtures;
using Xunit;

namespace ElysStay.Tests.Integration.Features;

public class ServiceCommandIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();
    private User _owner = null!;
    private Building _building = null!;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    private async Task SetupOwnerAndBuilding()
    {
        _owner = TestDataBuilder.CreateUser(role: UserRole.Owner);
        _building = TestDataBuilder.CreateBuilding(_owner.Id);

        await _fixture.DbContext.Users.AddAsync(_owner);
        await _fixture.DbContext.Buildings.AddAsync(_building);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateService_WithFourDecimalPrice_RoundsToTwoDecimals()
    {
        await SetupOwnerAndBuilding();

        var handler = new CreateServiceCommandHandler(
            _fixture.DbContext,
            new FakeCurrentUserService
            {
                UserId = _owner.Id,
                Role = UserRole.Owner,
                Email = _owner.Email,
                FullName = _owner.FullName
            });

        var result = await handler.Handle(new CreateServiceCommand
        {
            BuildingId = _building.Id,
            Name = "Internet",
            Unit = "month",
            UnitPrice = 1000.5678m,
            IsMetered = false
        }, CancellationToken.None);

        result.UnitPrice.Should().Be(1000.57m);

        var saved = _fixture.DbContext.Services.First(s => s.Id == result.Id);
        saved.UnitPrice.Should().Be(1000.57m);
    }

    [Fact]
    public async Task UpdateService_WithFourDecimalPrice_RoundsToTwoDecimals_AndTracksPreviousPrice()
    {
        await SetupOwnerAndBuilding();

        var service = TestDataBuilder.CreateService(_building.Id, name: "Water", unitPrice: 3500m);
        await _fixture.DbContext.Services.AddAsync(service);
        await _fixture.DbContext.SaveChangesAsync();

        var handler = new UpdateServiceCommandHandler(
            _fixture.DbContext,
            new FakeCurrentUserService
            {
                UserId = _owner.Id,
                Role = UserRole.Owner,
                Email = _owner.Email,
                FullName = _owner.FullName
            });

        var result = await handler.Handle(new UpdateServiceCommand
        {
            Id = service.Id,
            UnitPrice = 4000.9876m
        }, CancellationToken.None);

        result.UnitPrice.Should().Be(4000.99m);
        result.PreviousUnitPrice.Should().Be(3500m);
        result.PriceUpdatedAt.Should().NotBeNull();

        var updated = _fixture.DbContext.Services.First(s => s.Id == service.Id);
        updated.UnitPrice.Should().Be(4000.99m);
        updated.PreviousUnitPrice.Should().Be(3500m);
    }
}