using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Rooms.Commands;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using MockQueryable.Moq;
using Moq;
using Xunit;

namespace ElysStay.Tests.Unit.Business;

/// <summary>
/// Unit tests for ChangeRoomStatusCommandHandler.
/// Business rule SM-05: only Available ↔ Maintenance is allowed via manual PATCH.
/// Covers: post move-out maintenance flag, repairs completion, all rejection guards.
/// </summary>
public class ChangeRoomStatusUnitTests
{
    private readonly Mock<IApplicationDbContext> _db           = new();
    private readonly Mock<IBuildingScopeService> _buildingScope = new();

    private ChangeRoomStatusCommandHandler CreateHandler()
        => new(_db.Object, _buildingScope.Object);

    // ── Helpers ────────────────────────────────────────────────────────────────

    private Room CreateRoom(RoomStatus status = RoomStatus.Available)
    {
        var building = new Building { Id = Guid.NewGuid(), Name = "Tòa A" };
        return new Room
        {
            Id         = Guid.NewGuid(),
            BuildingId = building.Id,
            Building   = building,
            RoomNumber = "201",
            Status     = status
        };
    }

    private void SetupRoom(Room room)
    {
        _buildingScope
            .Setup(m => m.AuthorizeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _db.Setup(m => m.Rooms)
            .Returns(new List<Room> { room }.AsQueryable().BuildMockDbSet().Object);
        _db.Setup(m => m.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    // ── SM-05: Happy path — valid transitions ──────────────────────────────────

    [Fact]
    public async Task Handle_AvailableToMaintenance_ChangesStatusAndReturnsDto()
    {
        // Post move-out: owner sets Available room into Maintenance
        var room = CreateRoom(RoomStatus.Available);
        SetupRoom(room);

        var result = await CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = room.Id, Status = "Maintenance" }, default);

        result.Status.Should().Be("Maintenance");
        room.Status.Should().Be(RoomStatus.Maintenance,
            "room must be in Maintenance to prevent new reservations");
    }

    [Fact]
    public async Task Handle_MaintenanceToAvailable_ChangesStatusAndReturnsDto()
    {
        // Repairs done: owner sets room back to Available for new tenants
        var room = CreateRoom(RoomStatus.Maintenance);
        SetupRoom(room);

        var result = await CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = room.Id, Status = "Available" }, default);

        result.Status.Should().Be("Available");
        room.Status.Should().Be(RoomStatus.Available);
    }

    [Fact]
    public async Task Handle_CaseInsensitiveStatus_Accepted()
    {
        // Status parsing must be case-insensitive ("maintenance", "MAINTENANCE" both valid)
        var room = CreateRoom(RoomStatus.Available);
        SetupRoom(room);

        var result = await CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = room.Id, Status = "maintenance" }, default);

        result.Status.Should().Be("Maintenance");
    }

    // ── SM-05: Target status guard — only Available/Maintenance allowed ─────────

    [Fact]
    public async Task Handle_TargetStatusOccupied_ThrowsBadRequestException()
    {
        // Cannot manually set a room to Occupied — that is managed by contract lifecycle
        var room = CreateRoom(RoomStatus.Available);
        SetupRoom(room);

        var act = () => CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = room.Id, Status = "Occupied" }, default);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Trống hoặc Bảo trì*");
    }

    [Fact]
    public async Task Handle_TargetStatusBooked_ThrowsBadRequestException()
    {
        // Cannot manually set a room to Booked — that is managed by reservation lifecycle
        var room = CreateRoom(RoomStatus.Available);
        SetupRoom(room);

        var act = () => CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = room.Id, Status = "Booked" }, default);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Trống hoặc Bảo trì*");
    }

    [Fact]
    public async Task Handle_InvalidStatusString_ThrowsBadRequestException()
    {
        var room = CreateRoom(RoomStatus.Available);
        SetupRoom(room);

        var act = () => CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = room.Id, Status = "Unknown" }, default);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Trạng thái không hợp lệ*");
    }

    // ── SM-05: Current status guard — Occupied/Booked cannot be changed manually ─

    [Fact]
    public async Task Handle_OccupiedRoom_ThrowsConflictException()
    {
        // Room is currently Occupied (active tenant) — manual status change blocked
        var room = CreateRoom(RoomStatus.Occupied);
        SetupRoom(room);

        var act = () => CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = room.Id, Status = "Maintenance" }, default);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Trống hoặc Bảo trì*");
    }

    [Fact]
    public async Task Handle_BookedRoom_ThrowsConflictException()
    {
        // Room is Booked (reservation exists) — manual status change blocked
        var room = CreateRoom(RoomStatus.Booked);
        SetupRoom(room);

        var act = () => CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = room.Id, Status = "Maintenance" }, default);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Trống hoặc Bảo trì*");
    }

    // ── Same-status guard ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AlreadyMaintenance_ToMaintenance_ThrowsBadRequestException()
    {
        var room = CreateRoom(RoomStatus.Maintenance);
        SetupRoom(room);

        var act = () => CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = room.Id, Status = "Maintenance" }, default);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*đã ở trạng thái*");
    }

    [Fact]
    public async Task Handle_AlreadyAvailable_ToAvailable_ThrowsBadRequestException()
    {
        var room = CreateRoom(RoomStatus.Available);
        SetupRoom(room);

        var act = () => CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = room.Id, Status = "Available" }, default);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*đã ở trạng thái*");
    }

    // ── Not found guard ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_RoomNotFound_ThrowsNotFoundException()
    {
        _buildingScope
            .Setup(m => m.AuthorizeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _db.Setup(m => m.Rooms)
            .Returns(new List<Room>().AsQueryable().BuildMockDbSet().Object);

        var act = () => CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = Guid.NewGuid(), Status = "Maintenance" }, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Soft-deleted room is treated as not found ──────────────────────────────

    [Fact]
    public async Task Handle_SoftDeletedRoom_ThrowsNotFoundException()
    {
        var room = CreateRoom(RoomStatus.Available);
        room.DeletedAt = DateTime.UtcNow.AddDays(-1);  // soft-deleted
        SetupRoom(room);

        var act = () => CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = room.Id, Status = "Maintenance" }, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── DTO fields populated correctly ────────────────────────────────────────

    [Fact]
    public async Task Handle_SuccessfulChange_ReturnsDtoWithAllFields()
    {
        var room = CreateRoom(RoomStatus.Available);
        room.RoomNumber = "301";
        room.Floor = 3;
        SetupRoom(room);

        var result = await CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = room.Id, Status = "Maintenance" }, default);

        result.Id.Should().Be(room.Id);
        result.BuildingId.Should().Be(room.BuildingId);
        result.RoomNumber.Should().Be("301");
        result.Floor.Should().Be(3);
        result.Status.Should().Be("Maintenance");
    }

    // ── SaveChanges is called ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidChange_CallsSaveChangesOnce()
    {
        var room = CreateRoom(RoomStatus.Available);
        SetupRoom(room);

        await CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = room.Id, Status = "Maintenance" }, default);

        _db.Verify(m => m.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── UpdatedAt is stamped ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidChange_StampsUpdatedAt()
    {
        var room = CreateRoom(RoomStatus.Available);
        var before = DateTime.UtcNow.AddSeconds(-1);
        SetupRoom(room);

        await CreateHandler().Handle(
            new ChangeRoomStatusCommand { Id = room.Id, Status = "Maintenance" }, default);

        room.UpdatedAt.Should().BeAfter(before);
    }
}
