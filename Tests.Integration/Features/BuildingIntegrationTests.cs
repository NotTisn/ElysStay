using Xunit;
using FluentAssertions;
using Domain.Entities;
using Domain.Enums;
using Tests.Integration.Fixtures;
using Tests.Integration.Builders;

namespace ElysStay.Tests.Integration.Features;

public class BuildingIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();
    private User _owner = null!;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    private async Task SetupTestData()
    {
        _owner = TestDataBuilder.CreateUser(role: UserRole.Owner);
        await _fixture.DbContext.Users.AddAsync(_owner);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateBuilding_WithValidData_CreatesSuccessfully()
    {
        // Arrange
        await SetupTestData();
        var building = TestDataBuilder.CreateBuilding(_owner.Id, name: "Apartment Complex A");

        // Act
        await _fixture.DbContext.Buildings.AddAsync(building);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var saved = _fixture.DbContext.Buildings.FirstOrDefault(b => b.Id == building.Id);
        saved.Should().NotBeNull();
        saved!.OwnerId.Should().Be(_owner.Id);
    }

    [Fact]
    public async Task UpdateBuilding_ChangesDetails_UpdatesSuccessfully()
    {
        // Arrange
        await SetupTestData();
        var building = TestDataBuilder.CreateBuilding(_owner.Id);
        await _fixture.DbContext.Buildings.AddAsync(building);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        building.Name = "Updated Building Name";
        building.Description = "New description";
        _fixture.DbContext.Buildings.Update(building);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var updated = _fixture.DbContext.Buildings.FirstOrDefault(b => b.Id == building.Id);
        updated!.Name.Should().Be("Updated Building Name");
    }

    [Fact]
    public async Task GetBuildingsByOwner_FiltersByOwnerId_ReturnsOnlyOwnerBuildings()
    {
        // Arrange
        await SetupTestData();
        var owner2 = TestDataBuilder.CreateUser(role: UserRole.Owner);
        await _fixture.DbContext.Users.AddAsync(owner2);
        await _fixture.DbContext.SaveChangesAsync();

        var building1 = TestDataBuilder.CreateBuilding(_owner.Id);
        var building2 = TestDataBuilder.CreateBuilding(_owner.Id);
        var building3 = TestDataBuilder.CreateBuilding(owner2.Id);

        await _fixture.DbContext.Buildings.AddAsync(building1);
        await _fixture.DbContext.Buildings.AddAsync(building2);
        await _fixture.DbContext.Buildings.AddAsync(building3);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var ownerBuildings = _fixture.DbContext.Buildings
            .Where(b => b.OwnerId == _owner.Id)
            .ToList();

        // Assert
        ownerBuildings.Should().HaveCount(2);
        ownerBuildings.Should().AllSatisfy(b => b.OwnerId.Should().Be(_owner.Id));
    }

    [Fact]
    public async Task AssignStaffToBuilding_CreatesStaffAssignment()
    {
        // Arrange
        await SetupTestData();
        var building = TestDataBuilder.CreateBuilding(_owner.Id);
        var staff = TestDataBuilder.CreateUser(role: UserRole.Staff);

        await _fixture.DbContext.Buildings.AddAsync(building);
        await _fixture.DbContext.Users.AddAsync(staff);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var assignment = new StaffAssignment
        {
            BuildingId = building.Id,
            StaffId = staff.Id,
            AssignedAt = DateTime.UtcNow
        };

        await _fixture.DbContext.Set<StaffAssignment>().AddAsync(assignment);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var saved = _fixture.DbContext.Set<StaffAssignment>()
            .FirstOrDefault(a => a.BuildingId == building.Id && a.StaffId == staff.Id);
        saved.Should().NotBeNull();
    }
}
