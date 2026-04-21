using TechTalk.SpecFlow;
using Xunit;
using Domain.Entities;
using Domain.Enums;
using Tests.Integration.Fixtures;
using Tests.Integration.Builders;

namespace ElysStay.Tests.Acceptance.StepDefinitions;

[Binding]
public class RoomManagementSteps
{
    private readonly DatabaseFixture _fixture;
    private User _owner = null!;
    private Building _building = null!;
    private Room _room = null!;

    public RoomManagementSteps(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Given(@"a property owner exists")]
    public async Task GivenAPropertyOwnerExists()
    {
        _owner = TestDataBuilder.CreateUser(role: UserRole.Owner);
        await _fixture.DbContext.Users.AddAsync(_owner);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given(@"a building named ""(.*)"" exists")]
    public async Task GivenABuildingNamedExists(string buildingName)
    {
        _building = TestDataBuilder.CreateBuilding(_owner.Id, name: buildingName);
        await _fixture.DbContext.Buildings.AddAsync(_building);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [When(@"the owner adds a room ""(.*)"" with rent (.*) VND")]
    public async Task WhenTheOwnerAddsARoomWithRentVND(string roomNumber, decimal rent)
    {
        _room = TestDataBuilder.CreateRoom(_building.Id, roomNumber: roomNumber, price: rent);
        await _fixture.DbContext.Rooms.AddAsync(_room);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Then(@"the room should be saved successfully")]
    public void ThenTheRoomShouldBeSavedSuccessfully()
    {
        Assert.NotEqual(Guid.Empty, _room.Id);
        Assert.Equal(_building.Id, _room.BuildingId);
    }

    [Then(@"the room should be available for rent")]
    public void ThenTheRoomShouldBeAvailableForRent()
    {
        Assert.Equal(RoomStatus.Available, _room.Status);
    }
}
