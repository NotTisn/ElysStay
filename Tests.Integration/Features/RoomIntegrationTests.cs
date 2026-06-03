using Xunit;
using FluentAssertions;
using Domain.Entities;
using Domain.Enums;
using Tests.Integration.Fixtures;
using Tests.Integration.Builders;

namespace ElysStay.Tests.Integration.Features;

public class RoomIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();
    private User _owner = null!;
    private Building _building = null!;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    private async Task SetupTestData()
    {
        _owner = TestDataBuilder.CreateUser(role: UserRole.Owner);
        _building = TestDataBuilder.CreateBuilding(_owner.Id);

        await _fixture.DbContext.Users.AddAsync(_owner);
        await _fixture.DbContext.Buildings.AddAsync(_building);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateRoom_WithValidData_CreatesSuccessfully()
    {
        // Arrange
        await SetupTestData();
        var room = TestDataBuilder.CreateRoom(_building.Id, roomNumber: "101");

        // Act
        await _fixture.DbContext.Rooms.AddAsync(room);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var saved = _fixture.DbContext.Rooms.FirstOrDefault(r => r.Id == room.Id);
        saved.Should().NotBeNull();
        saved!.Status.Should().Be(RoomStatus.Available);
    }

    [Fact]
    public async Task UpdateRoom_ChangesPrice_UpdatesSuccessfully()
    {
        // Arrange
        await SetupTestData();
        var room = TestDataBuilder.CreateRoom(_building.Id, price: 5_000_000);
        await _fixture.DbContext.Rooms.AddAsync(room);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        const decimal newPrice = 6_000_000;
        room.Price = newPrice;
        _fixture.DbContext.Rooms.Update(room);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var updated = _fixture.DbContext.Rooms.FirstOrDefault(r => r.Id == room.Id);
        updated!.Price.Should().Be(newPrice);
    }

    [Fact]
    public async Task UpdateRoom_ChangesArea_UpdatesSuccessfully()
    {
        // Arrange
        await SetupTestData();
        var room = TestDataBuilder.CreateRoom(_building.Id);
        await _fixture.DbContext.Rooms.AddAsync(room);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        const decimal newArea = 35.75m;
        room.Area = newArea;
        _fixture.DbContext.Rooms.Update(room);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var updated = _fixture.DbContext.Rooms.FirstOrDefault(r => r.Id == room.Id);
        updated!.Area.Should().Be(newArea);
    }

    [Fact]
    public async Task UpdateRoomStatus_ToOccupied_UpdatesSuccessfully()
    {
        // Arrange
        await SetupTestData();
        var room = TestDataBuilder.CreateRoom(_building.Id);
        await _fixture.DbContext.Rooms.AddAsync(room);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        room.Status = RoomStatus.Occupied;
        _fixture.DbContext.Rooms.Update(room);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var updated = _fixture.DbContext.Rooms.FirstOrDefault(r => r.Id == room.Id);
        updated!.Status.Should().Be(RoomStatus.Occupied);
    }
    }
