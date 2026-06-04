using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Expenses.Commands;
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
/// Integration tests for expense recording, driven through the real
/// CreateExpenseCommandHandler against a real PostgreSQL database.
///
/// Business process: 1.5.8 — Expenses are operational costs stored in the Expense
/// table; they feed the "Expenses" bucket of the P&L report. Expense creation is
/// building-scoped (Owner/Staff). See <see cref="PnlReportIntegrationTests"/> for
/// how these expenses flow into Net Operating Profit / Net Cash Flow.
/// </summary>
public class ExpenseIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();

    private User _owner = null!;
    private Building _building = null!;
    private Room _room = null!;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    private async Task SetupTestData()
    {
        _owner = TestDataBuilder.CreateUser(role: UserRole.Owner);
        _building = TestDataBuilder.CreateBuilding(_owner.Id);
        _room = TestDataBuilder.CreateRoom(_building.Id);

        _fixture.DbContext.Users.Add(_owner);
        _fixture.DbContext.Buildings.Add(_building);
        _fixture.DbContext.Rooms.Add(_room);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    private CreateExpenseCommandHandler CreateHandler(Guid? ownerId = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(m => m.GetRequiredUserId()).Returns(ownerId ?? _owner.Id);
        currentUser.Setup(m => m.UserId).Returns(ownerId ?? _owner.Id);
        currentUser.Setup(m => m.IsOwner).Returns(true);
        currentUser.Setup(m => m.Role).Returns(UserRole.Owner);

        var scope = new BuildingScopeService(_fixture.DbContext, currentUser.Object);
        return new CreateExpenseCommandHandler(_fixture.DbContext, currentUser.Object, scope);
    }

    [Fact]
    public async Task CreateExpense_WithValidData_PersistsWithRecorder()
    {
        await SetupTestData();

        var result = await CreateHandler().Handle(new CreateExpenseCommand
        {
            BuildingId = _building.Id,
            Category = "Maintenance",
            Description = "Sửa mái nhà",
            Amount = 5_000_000,
            ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow)
        }, default);

        result.Category.Should().Be("Maintenance");
        result.Amount.Should().Be(5_000_000);
        result.RecordedBy.Should().Be(_owner.Id);

        var saved = await _fixture.DbContext.Expenses.FindAsync(result.Id);
        saved.Should().NotBeNull();
        saved!.BuildingId.Should().Be(_building.Id);
        saved.RecordedBy.Should().Be(_owner.Id);
    }

    [Fact]
    public async Task CreateExpense_ScopedToRoom_PersistsRoomLink()
    {
        await SetupTestData();

        var result = await CreateHandler().Handle(new CreateExpenseCommand
        {
            BuildingId = _building.Id,
            RoomId = _room.Id,
            Category = "Repair",
            Description = "Thay khóa cửa phòng 101",
            Amount = 800_000,
            ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow)
        }, default);

        result.RoomId.Should().Be(_room.Id);

        var saved = await _fixture.DbContext.Expenses.FindAsync(result.Id);
        saved!.RoomId.Should().Be(_room.Id);
    }

    [Fact]
    public async Task CreateExpense_RoomNotInBuilding_ThrowsNotFound()
    {
        await SetupTestData();

        // A room that belongs to a different building
        var otherOwner = TestDataBuilder.CreateUser(role: UserRole.Owner);
        var otherBuilding = TestDataBuilder.CreateBuilding(otherOwner.Id);
        var otherRoom = TestDataBuilder.CreateRoom(otherBuilding.Id, roomNumber: "999");
        _fixture.DbContext.Users.Add(otherOwner);
        _fixture.DbContext.Buildings.Add(otherBuilding);
        _fixture.DbContext.Rooms.Add(otherRoom);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        var act = () => CreateHandler().Handle(new CreateExpenseCommand
        {
            BuildingId = _building.Id,
            RoomId = otherRoom.Id,
            Category = "Repair",
            Description = "Sai phòng",
            Amount = 100_000,
            ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow)
        }, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateExpense_BuildingNotOwnedByUser_ThrowsForbidden()
    {
        await SetupTestData();

        // A different owner attempts to record an expense on this building
        var act = () => CreateHandler(ownerId: Guid.NewGuid()).Handle(new CreateExpenseCommand
        {
            BuildingId = _building.Id,
            Category = "Utilities",
            Description = "Tiền điện khu vực chung",
            Amount = 1_200_000,
            ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow)
        }, default);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
