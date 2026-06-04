using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Reservations.Commands;
using Application.Features.Reservations.DTOs;
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
/// Integration tests for the reservation lifecycle, driven through the real MediatR
/// command handlers against a real PostgreSQL database (Testcontainers).
///
/// Business processes under test:
///   • 1.5.1 Room Deposit Process     — owner creates reservation (Pending, room Reserved),
///                                       then confirms the deposit (Confirmed).
///   • 1.5.2 Reservation Cancellation — owner/tenant cancels with full/partial/no refund;
///                                       the room is released back to Available.
///
/// (Automatic cancellation in 1.5.2 is handled by ReservationExpiryBackgroundService and is
///  covered separately; the expiry guard it relies on is asserted here via the confirm path.)
/// </summary>
public class ReservationIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();

    private User _owner = null!;
    private User _tenant = null!;
    private Building _building = null!;
    private Room _room = null!;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    private async Task SetupTestData(RoomStatus roomStatus = RoomStatus.Available)
    {
        _owner = TestDataBuilder.CreateUser(role: UserRole.Owner);
        _tenant = TestDataBuilder.CreateUser(role: UserRole.Tenant);
        _building = TestDataBuilder.CreateBuilding(_owner.Id);
        _room = TestDataBuilder.CreateRoom(_building.Id);
        _room.Status = roomStatus;

        _fixture.DbContext.Users.AddRange(_owner, _tenant);
        _fixture.DbContext.Buildings.Add(_building);
        _fixture.DbContext.Rooms.Add(_room);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    // ── Handler wiring (acting as the building owner) ─────────────────────────

    private Mock<ICurrentUserService> OwnerIdentity()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(m => m.GetRequiredUserId()).Returns(_owner.Id);
        currentUser.Setup(m => m.UserId).Returns(_owner.Id);
        currentUser.Setup(m => m.IsOwner).Returns(true);
        currentUser.Setup(m => m.Role).Returns(UserRole.Owner);
        return currentUser;
    }

    private CreateReservationCommandHandler CreateReservationHandler()
    {
        var currentUser = OwnerIdentity();
        var scope = new BuildingScopeService(_fixture.DbContext, currentUser.Object);
        return new CreateReservationCommandHandler(_fixture.DbContext, currentUser.Object, scope);
    }

    private ChangeReservationStatusCommandHandler ChangeStatusHandler()
    {
        var currentUser = OwnerIdentity();
        var scope = new BuildingScopeService(_fixture.DbContext, currentUser.Object);
        return new ChangeReservationStatusCommandHandler(_fixture.DbContext, currentUser.Object, scope);
    }

    private Task<ReservationDto> CreateReservation(
        decimal deposit = 5_000_000, DateTime? expiresAt = null)
        => CreateReservationHandler().Handle(new CreateReservationCommand
        {
            RoomId = _room.Id,
            TenantUserId = _tenant.Id,
            DepositAmount = deposit,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(7)
        }, default);

    private Task<ReservationDto> Confirm(Guid id)
        => ChangeStatusHandler().Handle(
            new ChangeReservationStatusCommand { Id = id, Action = "CONFIRM" }, default);

    private Task<ReservationDto> Cancel(Guid id, decimal? refund, string? note = null)
        => ChangeStatusHandler().Handle(
            new ChangeReservationStatusCommand { Id = id, Action = "CANCEL", RefundAmount = refund, RefundNote = note }, default);

    // ── 1.5.1 Room Deposit Process — create ───────────────────────────────────

    [Fact]
    public async Task CreateReservation_AvailableRoom_IsPending_AndRoomBecomesReserved()
    {
        await SetupTestData(RoomStatus.Available);

        var dto = await CreateReservation(deposit: 5_000_000);

        dto.Status.Should().Be(ReservationStatus.Pending.ToString());
        dto.DepositAmount.Should().Be(5_000_000);

        var room = await _fixture.DbContext.Rooms.FindAsync(_room.Id);
        room!.Status.Should().Be(RoomStatus.Reserved);

        // DEP-02: deposit recorded on the reservation only — no Payment yet
        var payments = await _fixture.DbContext.Payments
            .Where(p => p.ReservationId == dto.Id).ToListAsync();
        payments.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateReservation_RoomNotAvailable_ThrowsConflict()
    {
        await SetupTestData(RoomStatus.Occupied);

        var act = () => CreateReservation();

        await act.Should().ThrowAsync<ConflictException>()
            .Where(e => e.ErrorCode == "ROOM_NOT_AVAILABLE");
    }

    [Fact]
    public async Task CreateReservation_TenantAlreadyHasActiveReservation_ThrowsConflict()
    {
        await SetupTestData(RoomStatus.Available);
        await CreateReservation();

        // A second room for the same tenant
        var room2 = TestDataBuilder.CreateRoom(_building.Id, roomNumber: "102");
        _fixture.DbContext.Rooms.Add(room2);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        var act = () => CreateReservationHandler().Handle(new CreateReservationCommand
        {
            RoomId = room2.Id,
            TenantUserId = _tenant.Id,
            DepositAmount = 5_000_000,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        }, default);

        await act.Should().ThrowAsync<ConflictException>()
            .Where(e => e.ErrorCode == "TENANT_HAS_ACTIVE_RESERVATION");
    }

    [Fact]
    public async Task CreateReservation_UserIsNotTenant_ThrowsBadRequest()
    {
        await SetupTestData(RoomStatus.Available);

        var act = () => CreateReservationHandler().Handle(new CreateReservationCommand
        {
            RoomId = _room.Id,
            TenantUserId = _owner.Id, // owner, not a tenant
            DepositAmount = 5_000_000,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        }, default);

        await act.Should().ThrowAsync<BadRequestException>();
    }

    // ── 1.5.1 Room Deposit Process — confirm ──────────────────────────────────

    [Fact]
    public async Task ConfirmReservation_PendingToConfirmed_RoomStaysReserved()
    {
        await SetupTestData(RoomStatus.Available);
        var reservation = await CreateReservation();

        var confirmed = await Confirm(reservation.Id);

        confirmed.Status.Should().Be(ReservationStatus.Confirmed.ToString());
        var room = await _fixture.DbContext.Rooms.FindAsync(_room.Id);
        room!.Status.Should().Be(RoomStatus.Reserved);
    }

    [Fact]
    public async Task ConfirmReservation_AlreadyConfirmed_ThrowsConflict()
    {
        await SetupTestData(RoomStatus.Available);
        var reservation = await CreateReservation();
        await Confirm(reservation.Id);

        var act = () => Confirm(reservation.Id);

        await act.Should().ThrowAsync<ConflictException>()
            .Where(e => e.ErrorCode == "INVALID_STATUS_TRANSITION");
    }

    [Fact]
    public async Task ConfirmReservation_Expired_ThrowsConflict()
    {
        await SetupTestData(RoomStatus.Available);
        // Create with an already-past expiry to simulate a stale reservation
        var reservation = await CreateReservation(expiresAt: DateTime.UtcNow.AddMinutes(-5));

        var act = () => Confirm(reservation.Id);

        await act.Should().ThrowAsync<ConflictException>()
            .Where(e => e.ErrorCode == "RESERVATION_EXPIRED");
    }

    // ── 1.5.2 Reservation Cancellation — confirmed reservations ───────────────

    [Fact]
    public async Task CancelConfirmedReservation_FullRefund_RecordsTwoPayments_AndFreesRoom()
    {
        await SetupTestData(RoomStatus.Available);
        var reservation = await CreateReservation(deposit: 5_000_000);
        await Confirm(reservation.Id);

        var cancelled = await Cancel(reservation.Id, refund: 5_000_000, note: "Hoàn cọc đầy đủ");

        cancelled.Status.Should().Be(ReservationStatus.Cancelled.ToString());

        var room = await _fixture.DbContext.Rooms.FindAsync(_room.Id);
        room!.Status.Should().Be(RoomStatus.Available);

        var payments = await _fixture.DbContext.Payments
            .Where(p => p.ReservationId == reservation.Id).ToListAsync();
        payments.Should().HaveCount(2);
        payments.Should().ContainSingle(p => p.Type == PaymentType.DepositIn && p.Amount == 5_000_000);
        payments.Should().ContainSingle(p => p.Type == PaymentType.DepositRefund && p.Amount == 5_000_000);
    }

    [Fact]
    public async Task CancelConfirmedReservation_ZeroRefund_ForfeitsDeposit_DepositInOnly()
    {
        await SetupTestData(RoomStatus.Available);
        var reservation = await CreateReservation(deposit: 5_000_000);
        await Confirm(reservation.Id);

        var cancelled = await Cancel(reservation.Id, refund: 0, note: "Cọc không hoàn theo chính sách");

        cancelled.Status.Should().Be(ReservationStatus.Cancelled.ToString());
        cancelled.RefundAmount.Should().Be(0);
        cancelled.RefundedAt.Should().BeNull();

        var payments = await _fixture.DbContext.Payments
            .Where(p => p.ReservationId == reservation.Id).ToListAsync();
        payments.Should().ContainSingle()
            .Which.Type.Should().Be(PaymentType.DepositIn);
        payments.Should().NotContain(p => p.Type == PaymentType.DepositRefund);
    }

    [Fact]
    public async Task CancelConfirmedReservation_PartialRefund_RecordsCorrectAmounts()
    {
        await SetupTestData(RoomStatus.Available);
        var reservation = await CreateReservation(deposit: 5_000_000);
        await Confirm(reservation.Id);

        await Cancel(reservation.Id, refund: 2_000_000, note: "Khấu trừ một phần");

        var payments = await _fixture.DbContext.Payments
            .Where(p => p.ReservationId == reservation.Id).ToListAsync();
        payments.Should().ContainSingle(p => p.Type == PaymentType.DepositIn && p.Amount == 5_000_000);
        payments.Should().ContainSingle(p => p.Type == PaymentType.DepositRefund && p.Amount == 2_000_000);
    }

    // ── 1.5.2 Reservation Cancellation — pending reservations ─────────────────

    [Fact]
    public async Task CancelPendingReservation_NoPayments_AndFreesRoom()
    {
        await SetupTestData(RoomStatus.Available);
        var reservation = await CreateReservation(deposit: 5_000_000);

        var cancelled = await Cancel(reservation.Id, refund: 0);

        cancelled.Status.Should().Be(ReservationStatus.Cancelled.ToString());

        var room = await _fixture.DbContext.Rooms.FindAsync(_room.Id);
        room!.Status.Should().Be(RoomStatus.Available);

        // Pending reservation never received a confirmed deposit → no Payment rows
        var payments = await _fixture.DbContext.Payments
            .Where(p => p.ReservationId == reservation.Id).ToListAsync();
        payments.Should().BeEmpty();
    }

    // ── 1.5.2 Reservation Cancellation — guards ───────────────────────────────

    [Fact]
    public async Task CancelReservation_RefundExceedsDeposit_ThrowsBadRequest()
    {
        await SetupTestData(RoomStatus.Available);
        var reservation = await CreateReservation(deposit: 5_000_000);
        await Confirm(reservation.Id);

        var act = () => Cancel(reservation.Id, refund: 6_000_000);

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task CancelReservation_AlreadyCancelled_ThrowsConflict()
    {
        await SetupTestData(RoomStatus.Available);
        var reservation = await CreateReservation(deposit: 5_000_000);
        await Confirm(reservation.Id);
        await Cancel(reservation.Id, refund: 0);

        var act = () => Cancel(reservation.Id, refund: 0);

        await act.Should().ThrowAsync<ConflictException>()
            .Where(e => e.ErrorCode == "INVALID_STATUS_TRANSITION");
    }
}
