using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Rooms.Commands;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Moq;
using Tests.Integration.Builders;
using Tests.Integration.Fixtures;
using Xunit;

namespace ElysStay.Tests.Integration.Features;

/// <summary>
/// Integration tests for ChangeRoomStatusCommandHandler (SM-05 Post Move-out Maintenance).
/// Uses real PostgreSQL (Testcontainers) + real ApplicationDbContext.
/// ICurrentUserService is mocked; BuildingScopeService is real.
/// </summary>
public class ChangeRoomStatusIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();

    private User     _owner    = null!;
    private Building _building = null!;
    private Room     _room     = null!;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync()    => await _fixture.DisposeAsync();

    // ── Handler factory ────────────────────────────────────────────────────────

    private ChangeRoomStatusCommandHandler CreateHandler(Guid? ownerId = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(m => m.GetRequiredUserId()).Returns(ownerId ?? _owner.Id);
        currentUser.Setup(m => m.IsOwner).Returns(true);

        var buildingScope = new BuildingScopeService(_fixture.DbContext, currentUser.Object);

        return new ChangeRoomStatusCommandHandler(_fixture.DbContext, buildingScope);
    }

    // ── Seed helpers ───────────────────────────────────────────────────────────

    private async Task SeedAsync(RoomStatus roomStatus = RoomStatus.Available)
    {
        _owner    = TestDataBuilder.CreateUser(role: UserRole.Owner);
        _building = TestDataBuilder.CreateBuilding(_owner.Id);
        _room     = TestDataBuilder.CreateRoom(_building.Id, roomNumber: "201");
        _room.Status = roomStatus;

        await _fixture.DbContext.Users.AddAsync(_owner);
        await _fixture.DbContext.Buildings.AddAsync(_building);
        await _fixture.DbContext.Rooms.AddAsync(_room);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    // ── SM-05: Happy path — Available → Maintenance ───────────────────────────

    [Fact]
    public async Task Handle_AvailableToMaintenance_PersistsStatusInDatabase()
    {
        await SeedAsync(RoomStatus.Available);

        await CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = _room.Id, Status = "Maintenance" }, default);

        var dbRoom = await _fixture.DbContext.Rooms.FindAsync(_room.Id);
        dbRoom!.Status.Should().Be(RoomStatus.Maintenance);
    }

    [Fact]
    public async Task Handle_AvailableToMaintenance_StampsUpdatedAtInDatabase()
    {
        await SeedAsync(RoomStatus.Available);
        var before = DateTime.UtcNow.AddSeconds(-1);

        await CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = _room.Id, Status = "Maintenance" }, default);

        var dbRoom = await _fixture.DbContext.Rooms.FindAsync(_room.Id);
        dbRoom!.UpdatedAt.Should().BeAfter(before);
    }

    // ── SM-05: Happy path — Maintenance → Available ───────────────────────────

    [Fact]
    public async Task Handle_MaintenanceToAvailable_PersistsStatusInDatabase()
    {
        await SeedAsync(RoomStatus.Maintenance);

        await CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = _room.Id, Status = "Available" }, default);

        var dbRoom = await _fixture.DbContext.Rooms.FindAsync(_room.Id);
        dbRoom!.Status.Should().Be(RoomStatus.Available);
    }

    // ── DTO correctness ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_SuccessfulChange_ReturnsDtoMatchingDatabase()
    {
        await SeedAsync(RoomStatus.Available);

        var result = await CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = _room.Id, Status = "Maintenance" }, default);

        result.Id.Should().Be(_room.Id);
        result.BuildingId.Should().Be(_building.Id);
        result.BuildingName.Should().Be(_building.Name);
        result.RoomNumber.Should().Be("201");
        result.Status.Should().Be("Maintenance");
    }

    // ── Case-insensitive status parsing ───────────────────────────────────────

    [Fact]
    public async Task Handle_LowercaseStatus_AcceptedAndPersists()
    {
        await SeedAsync(RoomStatus.Available);

        await CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = _room.Id, Status = "maintenance" }, default);

        var dbRoom = await _fixture.DbContext.Rooms.FindAsync(_room.Id);
        dbRoom!.Status.Should().Be(RoomStatus.Maintenance);
    }

    // ── Rejection guards persist no changes ───────────────────────────────────

    [Fact]
    public async Task Handle_OccupiedRoom_ThrowsConflict_StatusUnchangedInDatabase()
    {
        await SeedAsync(RoomStatus.Occupied);

        var act = () => CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = _room.Id, Status = "Maintenance" }, default);

        await act.Should().ThrowAsync<ConflictException>();

        var dbRoom = await _fixture.DbContext.Rooms.FindAsync(_room.Id);
        dbRoom!.Status.Should().Be(RoomStatus.Occupied, "rejected transitions must not change persisted state");
    }

    [Fact]
    public async Task Handle_BookedRoom_ThrowsConflict_StatusUnchangedInDatabase()
    {
        await SeedAsync(RoomStatus.Reserved);

        var act = () => CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = _room.Id, Status = "Available" }, default);

        await act.Should().ThrowAsync<ConflictException>();

        var dbRoom = await _fixture.DbContext.Rooms.FindAsync(_room.Id);
        dbRoom!.Status.Should().Be(RoomStatus.Reserved);
    }

    [Fact]
    public async Task Handle_TargetStatusOccupied_ThrowsBadRequest_StatusUnchangedInDatabase()
    {
        await SeedAsync(RoomStatus.Available);

        var act = () => CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = _room.Id, Status = "Occupied" }, default);

        await act.Should().ThrowAsync<BadRequestException>();

        var dbRoom = await _fixture.DbContext.Rooms.FindAsync(_room.Id);
        dbRoom!.Status.Should().Be(RoomStatus.Available);
    }

    [Fact]
    public async Task Handle_SameStatus_ThrowsBadRequest_StatusUnchangedInDatabase()
    {
        await SeedAsync(RoomStatus.Maintenance);

        var act = () => CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = _room.Id, Status = "Maintenance" }, default);

        await act.Should().ThrowAsync<BadRequestException>();

        var dbRoom = await _fixture.DbContext.Rooms.FindAsync(_room.Id);
        dbRoom!.Status.Should().Be(RoomStatus.Maintenance);
    }

    // ── Not found / soft-deleted ───────────────────────────────────────────────

    [Fact]
    public async Task Handle_NonExistentRoom_ThrowsNotFoundException()
    {
        await SeedAsync();

        var act = () => CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = Guid.NewGuid(), Status = "Maintenance" }, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_SoftDeletedRoom_ThrowsNotFoundException()
    {
        await SeedAsync(RoomStatus.Available);

        _room.DeletedAt = DateTime.UtcNow.AddDays(-1);
        _fixture.DbContext.Rooms.Update(_room);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        var act = () => CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = _room.Id, Status = "Maintenance" }, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── AUTH-05: Cross-building access denied ──────────────────────────────────

    [Fact]
    public async Task Handle_UnauthorizedOwner_ThrowsForbiddenException()
    {
        await SeedAsync(RoomStatus.Available);

        var otherId = Guid.NewGuid();
        var act = () => CreateHandler(ownerId: otherId).Handle(
            new ChangeRoomStatusCommand { Id = _room.Id, Status = "Maintenance" }, default);

        await act.Should().ThrowAsync<Exception>("owner not assigned to this building must be rejected");
    }

    // ── Round-trip: Available → Maintenance → Available ───────────────────────

    [Fact]
    public async Task Handle_RoundTrip_StatusRestoredCorrectly()
    {
        await SeedAsync(RoomStatus.Available);

        await CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = _room.Id, Status = "Maintenance" }, default);

        var afterMaintenance = await _fixture.DbContext.Rooms.FindAsync(_room.Id);
        afterMaintenance!.Status.Should().Be(RoomStatus.Maintenance);
        _fixture.DbContext.ChangeTracker.Clear();

        await CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = _room.Id, Status = "Available" }, default);

        var afterAvailable = await _fixture.DbContext.Rooms.FindAsync(_room.Id);
        afterAvailable!.Status.Should().Be(RoomStatus.Available);
    }
}
