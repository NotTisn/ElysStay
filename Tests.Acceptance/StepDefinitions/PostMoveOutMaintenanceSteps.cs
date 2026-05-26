using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Rooms.Commands;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Auth;
using Moq;
using TechTalk.SpecFlow;
using Tests.Integration.Builders;
using Tests.Integration.Fixtures;

namespace ElysStay.Tests.Acceptance.StepDefinitions;

/// <summary>
/// Step definitions for PostMoveOutMaintenance.feature.
/// Uses real ChangeRoomStatusCommandHandler against a live PostgreSQL container.
/// ICurrentUserService is mocked; BuildingScopeService is real.
/// </summary>
[Binding]
[Scope(Feature = "Post Move-out Maintenance Process")]
public class PostMoveOutMaintenanceSteps
{
    private readonly DatabaseFixture _fixture;

    private User     _owner    = null!;
    private Building _building = null!;
    private Room     _room     = null!;

    private Exception? _lastException;

    public PostMoveOutMaintenanceSteps(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    // ── Handler factory ────────────────────────────────────────────────────────

    private ChangeRoomStatusCommandHandler CreateHandler()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(m => m.GetRequiredUserId()).Returns(_owner.Id);
        currentUser.Setup(m => m.IsOwner).Returns(true);

        return new ChangeRoomStatusCommandHandler(
            _fixture.DbContext,
            new BuildingScopeService(_fixture.DbContext, currentUser.Object));
    }

    // ── Background steps ───────────────────────────────────────────────────────

    [Given("a building owner for maintenance tests")]
    public async Task GivenABuildingOwnerForMaintenanceTests()
    {
        _owner = TestDataBuilder.CreateUser(role: UserRole.Owner);
        await _fixture.DbContext.Users.AddAsync(_owner);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given("a building for maintenance tests")]
    public async Task GivenABuildingForMaintenanceTests()
    {
        _building = TestDataBuilder.CreateBuilding(_owner.Id);
        await _fixture.DbContext.Buildings.AddAsync(_building);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given("a room \"([^\"]*)\" in Available status")]
    public async Task GivenARoomInAvailableStatus(string roomNumber)
    {
        _room = TestDataBuilder.CreateRoom(_building.Id, roomNumber: roomNumber);
        _room.Status = RoomStatus.Available;
        await _fixture.DbContext.Rooms.AddAsync(_room);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    // ── Given — set current room status ───────────────────────────────────────

    [Given("the room is in Maintenance status")]
    public async Task GivenTheRoomIsInMaintenanceStatus()
    {
        _room.Status = RoomStatus.Maintenance;
        _fixture.DbContext.Rooms.Update(_room);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    [Given("the room is in Occupied status")]
    public async Task GivenTheRoomIsInOccupiedStatus()
    {
        _room.Status = RoomStatus.Occupied;
        _fixture.DbContext.Rooms.Update(_room);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    [Given("the room is in Booked status")]
    public async Task GivenTheRoomIsInBookedStatus()
    {
        _room.Status = RoomStatus.Booked;
        _fixture.DbContext.Rooms.Update(_room);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    // ── When steps ─────────────────────────────────────────────────────────────

    [When("I change the room status to \"([^\"]*)\"")]
    public async Task WhenIChangeTheRoomStatusTo(string status)
    {
        _lastException = null;
        await CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = _room.Id, Status = status }, default);
        _fixture.DbContext.ChangeTracker.Clear();
    }

    [When("I try to change the room status to \"([^\"]*)\"")]
    public async Task WhenITryToChangeTheRoomStatusTo(string status)
    {
        _lastException = null;
        try
        {
            await CreateHandler().Handle(
                new ChangeRoomStatusCommand { Id = _room.Id, Status = status }, default);
            _fixture.DbContext.ChangeTracker.Clear();
        }
        catch (Exception ex)
        {
            _lastException = ex;
        }
    }

    // ── Then — room status in database ────────────────────────────────────────

    [Then("the room status in the database should be \"([^\"]*)\"")]
    public async Task ThenTheRoomStatusInDatabaseShouldBe(string status)
    {
        var expected = Enum.Parse<RoomStatus>(status);
        var dbRoom = await _fixture.DbContext.Rooms.FindAsync(_room.Id);
        dbRoom!.Status.Should().Be(expected);
    }

    // ── Then — error assertions ────────────────────────────────────────────────

    [Then("the status change should be rejected with a bad request error")]
    public void ThenStatusChangeRejectedWithBadRequest()
    {
        _lastException.Should().NotBeNull("a bad request exception must have been thrown");
        _lastException.Should().BeOfType<BadRequestException>();
    }

    [Then("the status change should be rejected with a conflict error")]
    public void ThenStatusChangeRejectedWithConflict()
    {
        _lastException.Should().NotBeNull("a conflict exception must have been thrown");
        _lastException.Should().BeOfType<ConflictException>();
    }
}
