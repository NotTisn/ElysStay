using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Contracts.Commands;
using Application.Features.Reservations.Commands;
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
/// End-to-end integration tests for the communication between the
/// <b>reservation</b> and <b>contract</b> features. These tests drive the real
/// MediatR command handlers against a real PostgreSQL database (Testcontainers),
/// so they verify the cross-feature hand-off, not just raw persistence.
///
/// Business processes under test:
///   • 1.5.1 Room Deposit Process       — create reservation (Pending, room Reserved) → confirm (Confirmed).
///   • 1.5.2 Reservation Cancellation   — cancel frees the room and settles the deposit.
///   • 1.5.3 Contract Creation Process  — confirmed reservation → contract: reservation Converted,
///                                         deposit carried over, top-up if insufficient, room Occupied.
/// </summary>
public class ContractIntegrationTests : IAsyncLifetime
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

        _fixture.DbContext.Users.AddRange(_owner, _tenant);
        _fixture.DbContext.Buildings.Add(_building);
        _fixture.DbContext.Rooms.Add(_room);
        await _fixture.DbContext.SaveChangesAsync();
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

    private static Mock<IEmailService> NoopEmail()
    {
        var emailService = new Mock<IEmailService>();
        emailService
            .Setup(m => m.TrySendAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return emailService;
    }

    private CreateReservationCommandHandler CreateReservationHandler()
    {
        var currentUser = OwnerIdentity();
        var scope = new BuildingScopeService(_fixture.DbContext, currentUser.Object);
        return new CreateReservationCommandHandler(_fixture.DbContext, currentUser.Object, scope);
    }

    private ChangeReservationStatusCommandHandler ChangeReservationStatusHandler()
    {
        var currentUser = OwnerIdentity();
        var scope = new BuildingScopeService(_fixture.DbContext, currentUser.Object);
        return new ChangeReservationStatusCommandHandler(_fixture.DbContext, currentUser.Object, scope);
    }

    private CreateContractCommandHandler CreateContractHandler()
    {
        var currentUser = OwnerIdentity();
        var scope = new BuildingScopeService(_fixture.DbContext, currentUser.Object);
        return new CreateContractCommandHandler(_fixture.DbContext, currentUser.Object, scope, NoopEmail().Object);
    }

    // ── Flow helpers ──────────────────────────────────────────────────────────

    /// <summary>1.5.1 — create a Pending reservation (room becomes Reserved).</summary>
    private Task<Application.Features.Reservations.DTOs.ReservationDto> CreatePendingReservation(decimal deposit = 5_000_000)
        => CreateReservationHandler().Handle(new CreateReservationCommand
        {
            RoomId = _room.Id,
            TenantUserId = _tenant.Id,
            DepositAmount = deposit,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        }, default);

    /// <summary>1.5.1 — owner confirms the deposit (Pending → Confirmed).</summary>
    private Task<Application.Features.Reservations.DTOs.ReservationDto> ConfirmReservation(Guid reservationId)
        => ChangeReservationStatusHandler().Handle(new ChangeReservationStatusCommand
        {
            Id = reservationId,
            Action = "CONFIRM"
        }, default);

    private CreateContractCommand ContractFromReservation(Guid reservationId, decimal contractDeposit)
    {
        var start = DateOnly.FromDateTime(DateTime.UtcNow);
        return new CreateContractCommand
        {
            RoomId = _room.Id,
            TenantUserId = _tenant.Id,
            ReservationId = reservationId,
            StartDate = start,
            MoveInDate = start,
            EndDate = start.AddMonths(12),
            MonthlyRent = _room.Price,
            DepositAmount = contractDeposit
        };
    }

    // ── 1.5.1 Room Deposit Process ────────────────────────────────────────────

    [Fact]
    public async Task CreateReservation_StartsPending_AndRoomBecomesReserved()
    {
        // Arrange
        await SetupTestData();

        // Act — 1.5.1: owner/staff creates the reservation request
        var dto = await CreatePendingReservation(deposit: 5_000_000);

        // Assert — reservation is Pending, deposit recorded on reservation (no Payment yet)
        dto.Status.Should().Be(ReservationStatus.Pending.ToString());
        dto.DepositAmount.Should().Be(5_000_000);

        var room = await _fixture.DbContext.Rooms.FindAsync(_room.Id);
        room!.Status.Should().Be(RoomStatus.Reserved);

        // DEP-02: no payment is created at reservation time
        var payments = await _fixture.DbContext.Payments
            .Where(p => p.ReservationId == dto.Id).ToListAsync();
        payments.Should().BeEmpty();
    }

    [Fact]
    public async Task ConfirmReservation_TransitionsPendingToConfirmed()
    {
        // Arrange
        await SetupTestData();
        var reservation = await CreatePendingReservation();

        // Act — 1.5.1: owner verifies the transfer and confirms
        var confirmed = await ConfirmReservation(reservation.Id);

        // Assert
        confirmed.Status.Should().Be(ReservationStatus.Confirmed.ToString());

        // Room stays Reserved while the deposit is held
        var room = await _fixture.DbContext.Rooms.FindAsync(_room.Id);
        room!.Status.Should().Be(RoomStatus.Reserved);
    }

    // ── 1.5.2 Reservation Cancellation Process ────────────────────────────────

    [Fact]
    public async Task CancelConfirmedReservation_FreesRoom_AndRefundsDeposit()
    {
        // Arrange
        await SetupTestData();
        var reservation = await CreatePendingReservation(deposit: 5_000_000);
        await ConfirmReservation(reservation.Id);

        // Act — 1.5.2: owner cancels with a full refund
        var cancelled = await ChangeReservationStatusHandler().Handle(new ChangeReservationStatusCommand
        {
            Id = reservation.Id,
            Action = "CANCEL",
            RefundAmount = 5_000_000,
            RefundNote = "Khách đổi ý, hoàn cọc đầy đủ"
        }, default);

        // Assert — reservation cancelled, room released for other tenants
        cancelled.Status.Should().Be(ReservationStatus.Cancelled.ToString());

        var room = await _fixture.DbContext.Rooms.FindAsync(_room.Id);
        room!.Status.Should().Be(RoomStatus.Available);

        // A DEPOSIT_IN (received) and a DEPOSIT_REFUND (returned) are recorded
        var payments = await _fixture.DbContext.Payments
            .Where(p => p.ReservationId == reservation.Id).ToListAsync();
        payments.Should().HaveCount(2);
        payments.Should().ContainSingle(p => p.Type == PaymentType.DepositIn && p.Amount == 5_000_000);
        payments.Should().ContainSingle(p => p.Type == PaymentType.DepositRefund && p.Amount == 5_000_000);
    }

    // ── 1.5.3 Contract Creation Process (reservation → contract hand-off) ──────

    [Fact]
    public async Task CreateContract_FromConfirmedReservation_ConvertsReservation_AndOccupiesRoom()
    {
        // Arrange — full 1.5.1 flow
        await SetupTestData();
        var reservation = await CreatePendingReservation(deposit: 5_000_000);
        await ConfirmReservation(reservation.Id);

        // Act — 1.5.3: tenant signs, contract created from the confirmed reservation
        var contract = await CreateContractHandler().Handle(
            ContractFromReservation(reservation.Id, contractDeposit: 5_000_000), default);

        // Assert — the contract is linked back to the reservation
        contract.ReservationId.Should().Be(reservation.Id);
        contract.Status.Should().Be(ContractStatus.Active.ToString());

        // Reservation status changes to Converted
        var persistedReservation = await _fixture.DbContext.RoomReservations.FindAsync(reservation.Id);
        persistedReservation!.Status.Should().Be(ReservationStatus.Converted);

        // Room status changes to Occupied
        var room = await _fixture.DbContext.Rooms.FindAsync(_room.Id);
        room!.Status.Should().Be(RoomStatus.Occupied);

        // The reservation deposit is officially transferred into the contract deposit
        var payments = await _fixture.DbContext.Payments
            .Where(p => p.ContractId == contract.Id).ToListAsync();
        payments.Should().ContainSingle(p => p.Type == PaymentType.DepositIn && p.Amount == 5_000_000)
            .Which.Note.Should().Contain("chuyển từ đặt phòng");
    }

    [Fact]
    public async Task CreateContract_WhenContractDepositExceedsReservation_RecordsTopUpPayment()
    {
        // Arrange — reservation deposit is 5,000,000 …
        await SetupTestData();
        var reservation = await CreatePendingReservation(deposit: 5_000_000);
        await ConfirmReservation(reservation.Id);

        // Act — … but the contract requires an 8,000,000 deposit (1.5.3: pay the remaining amount)
        var contract = await CreateContractHandler().Handle(
            ContractFromReservation(reservation.Id, contractDeposit: 8_000_000), default);

        // Assert — two DEPOSIT_IN payments: the carry-over + the top-up
        var payments = await _fixture.DbContext.Payments
            .Where(p => p.ContractId == contract.Id && p.Type == PaymentType.DepositIn)
            .ToListAsync();
        payments.Should().HaveCount(2);
        payments.Should().ContainSingle(p => p.Amount == 5_000_000);   // carried from reservation
        payments.Should().ContainSingle(p => p.Amount == 3_000_000);   // remaining top-up
        payments.Sum(p => p.Amount).Should().Be(8_000_000);
    }

    [Fact]
    public async Task CreateContract_FromPendingReservation_IsRejected()
    {
        // Arrange — reservation created but NOT confirmed
        await SetupTestData();
        var reservation = await CreatePendingReservation(deposit: 5_000_000);

        // Act — 1.5.3 requires a Confirmed reservation before a contract may be created
        var act = () => CreateContractHandler().Handle(
            ContractFromReservation(reservation.Id, contractDeposit: 5_000_000), default);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Xác nhận*");

        // The reservation is untouched and the room is still Reserved
        var persistedReservation = await _fixture.DbContext.RoomReservations.FindAsync(reservation.Id);
        persistedReservation!.Status.Should().Be(ReservationStatus.Pending);
        var room = await _fixture.DbContext.Rooms.FindAsync(_room.Id);
        room!.Status.Should().Be(RoomStatus.Reserved);
    }

    [Fact]
    public async Task CreateContract_WhenContractDepositBelowReservation_IsRejected()
    {
        // Arrange
        await SetupTestData();
        var reservation = await CreatePendingReservation(deposit: 5_000_000);
        await ConfirmReservation(reservation.Id);

        // Act — contract deposit (3,000,000) is lower than the reservation deposit (5,000,000)
        var act = () => CreateContractHandler().Handle(
            ContractFromReservation(reservation.Id, contractDeposit: 3_000_000), default);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*không được thấp hơn*");

        // Reservation remains Confirmed (not converted) and room remains Reserved
        var persistedReservation = await _fixture.DbContext.RoomReservations.FindAsync(reservation.Id);
        persistedReservation!.Status.Should().Be(ReservationStatus.Confirmed);
        var room = await _fixture.DbContext.Rooms.FindAsync(_room.Id);
        room!.Status.Should().Be(RoomStatus.Reserved);
    }

    [Fact]
    public async Task FullDepositFlow_Reserve_Confirm_Contract_EndToEnd()
    {
        // Arrange
        await SetupTestData();

        // 1.5.1 — create reservation: Pending + room Reserved
        var reservation = await CreatePendingReservation(deposit: 5_000_000);
        reservation.Status.Should().Be(ReservationStatus.Pending.ToString());
        (await _fixture.DbContext.Rooms.FindAsync(_room.Id))!.Status.Should().Be(RoomStatus.Reserved);

        // 1.5.1 — confirm deposit: Confirmed
        var confirmed = await ConfirmReservation(reservation.Id);
        confirmed.Status.Should().Be(ReservationStatus.Confirmed.ToString());

        // 1.5.3 — create contract: reservation Converted + room Occupied + deposit carried over
        var contract = await CreateContractHandler().Handle(
            ContractFromReservation(reservation.Id, contractDeposit: 5_000_000), default);

        contract.ReservationId.Should().Be(reservation.Id);
        (await _fixture.DbContext.RoomReservations.FindAsync(reservation.Id))!.Status
            .Should().Be(ReservationStatus.Converted);
        (await _fixture.DbContext.Rooms.FindAsync(_room.Id))!.Status.Should().Be(RoomStatus.Occupied);

        var depositPayments = await _fixture.DbContext.Payments
            .Where(p => p.ContractId == contract.Id && p.Type == PaymentType.DepositIn)
            .ToListAsync();
        depositPayments.Sum(p => p.Amount).Should().Be(5_000_000);

        // A new contract may not be created on the now-Occupied room (UQ-01)
        var secondAttempt = () => CreateContractHandler().Handle(new CreateContractCommand
        {
            RoomId = _room.Id,
            TenantUserId = _tenant.Id,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MoveInDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(12),
            MonthlyRent = _room.Price,
            DepositAmount = 5_000_000
        }, default);

        await secondAttempt.Should().ThrowAsync<ConflictException>();
    }
}
