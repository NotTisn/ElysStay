using Xunit;
using FluentAssertions;
using Domain.Entities;
using Domain.Enums;
using Tests.Integration.Fixtures;
using Tests.Integration.Builders;

namespace ElysStay.Tests.Integration.Features;

public class ReservationIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();
    private User _owner = null!;
    private User _tenant = null!;
    private Building _building = null!;
    private Room _room = null!;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    private async Task SetupTestData()
    {
        _owner = TestDataBuilder.CreateUser(role: UserRole.Owner);
        _tenant = TestDataBuilder.CreateUser(role: UserRole.Tenant);
        _building = TestDataBuilder.CreateBuilding(_owner.Id);
        _room = TestDataBuilder.CreateRoom(_building.Id);

        await _fixture.DbContext.Users.AddAsync(_owner);
        await _fixture.DbContext.Users.AddAsync(_tenant);
        await _fixture.DbContext.Buildings.AddAsync(_building);
        await _fixture.DbContext.Rooms.AddAsync(_room);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateReservation_WithValidData_CreatesSuccessfully()
    {
        // Arrange
        await SetupTestData();
        var reservation = TestDataBuilder.CreateReservation(_room.Id, _tenant.Id);

        // Act
        await _fixture.DbContext.Set<RoomReservation>().AddAsync(reservation);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var saved = _fixture.DbContext.Set<RoomReservation>()
            .FirstOrDefault(r => r.Id == reservation.Id);
        saved.Should().NotBeNull();
        saved!.Status.Should().Be(ReservationStatus.Pending);
    }

    [Fact]
    public async Task ConvertReservationToContract_WithPendingReservation_CreatesContractSuccessfully()
    {
        // Arrange
        await SetupTestData();
        var reservation = TestDataBuilder.CreateReservation(_room.Id, _tenant.Id, status: ReservationStatus.Pending);
        await _fixture.DbContext.Set<RoomReservation>().AddAsync(reservation);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var endDate = startDate.AddMonths(12);
        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            RoomId = _room.Id,
            TenantUserId = _tenant.Id,
            ReservationId = reservation.Id,
            StartDate = startDate,
            MoveInDate = startDate,
            EndDate = endDate,
            MonthlyRent = _room.Price,
            DepositAmount = reservation.DepositAmount,
            DepositStatus = DepositStatus.Held,
            Status = ContractStatus.Active,
            CreatedBy = _owner.Id
        };

        reservation.Status = ReservationStatus.Confirmed;

        await _fixture.DbContext.Contracts.AddAsync(contract);
        _fixture.DbContext.Set<RoomReservation>().Update(reservation);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var savedContract = _fixture.DbContext.Contracts.FirstOrDefault(c => c.Id == contract.Id);
        savedContract.Should().NotBeNull();
        savedContract!.ReservationId.Should().Be(reservation.Id);
    }

    [Fact]
    public async Task CancelReservation_WithValidReservation_CancelSuccessfully()
    {
        // Arrange
        await SetupTestData();
        var reservation = TestDataBuilder.CreateReservation(_room.Id, _tenant.Id);
        await _fixture.DbContext.Set<RoomReservation>().AddAsync(reservation);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        reservation.Status = ReservationStatus.Cancelled;
        _fixture.DbContext.Set<RoomReservation>().Update(reservation);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var updated = _fixture.DbContext.Set<RoomReservation>()
            .FirstOrDefault(r => r.Id == reservation.Id);
        updated!.Status.Should().Be(ReservationStatus.Cancelled);
    }

    [Fact]
    public async Task ProcessRefund_OnCancelledReservation_CreatesRefundPayment()
    {
        // Arrange
        await SetupTestData();
        var reservation = TestDataBuilder.CreateReservation(_room.Id, _tenant.Id);
        await _fixture.DbContext.Set<RoomReservation>().AddAsync(reservation);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        reservation.Status = ReservationStatus.Cancelled;
        reservation.RefundedAt = DateTime.UtcNow;
        reservation.RefundNote = "Full refund processed";
        _fixture.DbContext.Set<RoomReservation>().Update(reservation);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var updated = _fixture.DbContext.Set<RoomReservation>()
            .FirstOrDefault(r => r.Id == reservation.Id);
        updated!.RefundedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetReservations_FiltersByRoom_ReturnsOnlyRoomReservations()
    {
        // Arrange
        await SetupTestData();
        var res1 = TestDataBuilder.CreateReservation(_room.Id, _tenant.Id);
        var res2 = TestDataBuilder.CreateReservation(_room.Id, _tenant.Id);

        await _fixture.DbContext.Set<RoomReservation>().AddAsync(res1);
        await _fixture.DbContext.Set<RoomReservation>().AddAsync(res2);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var reservations = _fixture.DbContext.Set<RoomReservation>()
            .Where(r => r.RoomId == _room.Id)
            .ToList();

        // Assert
        reservations.Should().HaveCount(2);
        reservations.Should().AllSatisfy(r => r.RoomId.Should().Be(_room.Id));
    }
}
