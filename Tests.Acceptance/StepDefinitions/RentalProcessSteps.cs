using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Contracts.Commands;
using Application.Features.Contracts.DTOs;
using Application.Features.Reservations.Commands;
using Application.Features.Reservations.DTOs;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Moq;
using TechTalk.SpecFlow;
using Tests.Integration.Builders;
using Tests.Integration.Fixtures;

namespace ElysStay.Tests.Acceptance.StepDefinitions;

/// <summary>
/// Step definitions for RentalProcess.feature.
/// Covers: direct contract, reservation → confirm → contract, cancellation, double-booking prevention.
/// </summary>
[Binding]
[Scope(Feature = "Rental Process")]
public class RentalProcessSteps
{
    private readonly DatabaseFixture _fixture;

    private User     _owner    = null!;
    private Building _building = null!;
    private Room     _room     = null!;
    private Room?    _room2;
    private User     _tenant   = null!;
    private User?    _tenant2;

    private ReservationDto? _reservationResult;
    private ContractDto?    _contractResult;
    private Exception?      _lastException;

    // Track the active reservation for multi-step scenarios
    private Guid _activeReservationId;

    public RentalProcessSteps(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    // ── Handler factories ──────────────────────────────────────────────────────

    private CreateReservationCommandHandler ReservationHandler()
    {
        var currentUser = MockOwner();
        return new CreateReservationCommandHandler(
            _fixture.DbContext, currentUser,
            new BuildingScopeService(_fixture.DbContext, currentUser));
    }

    private ChangeReservationStatusCommandHandler ReservationStatusHandler()
    {
        var currentUser = MockOwner();
        return new ChangeReservationStatusCommandHandler(
            _fixture.DbContext, currentUser,
            new BuildingScopeService(_fixture.DbContext, currentUser));
    }

    private CreateContractCommandHandler ContractHandler()
    {
        var currentUser = MockOwner();
        var emailService = new Mock<IEmailService>();
        emailService.Setup(m => m.TrySendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        return new CreateContractCommandHandler(
            _fixture.DbContext, currentUser,
            new BuildingScopeService(_fixture.DbContext, currentUser),
            emailService.Object);
    }

    private ICurrentUserService MockOwner()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(m => m.GetRequiredUserId()).Returns(_owner.Id);
        currentUser.Setup(m => m.IsOwner).Returns(true);
        return currentUser.Object;
    }

    // ── Background steps ───────────────────────────────────────────────────────

    [Given("a building owner for rental tests")]
    public async Task GivenABuildingOwnerForRentalTests()
    {
        _owner = TestDataBuilder.CreateUser(role: UserRole.Owner);
        await _fixture.DbContext.Users.AddAsync(_owner);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given("a building for rental tests")]
    public async Task GivenABuildingForRentalTests()
    {
        _building = TestDataBuilder.CreateBuilding(_owner.Id);
        await _fixture.DbContext.Buildings.AddAsync(_building);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given("an available room \"([^\"]*)\" priced at ([0-9]+) VND for rental tests")]
    public async Task GivenAnAvailableRoomForRentalTests(string roomNumber, decimal price)
    {
        _room = TestDataBuilder.CreateRoom(_building.Id, roomNumber: roomNumber, price: price);
        _room.Status = RoomStatus.Available;
        await _fixture.DbContext.Rooms.AddAsync(_room);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    [Given("a tenant for rental tests")]
    public async Task GivenATenantForRentalTests()
    {
        _tenant = TestDataBuilder.CreateUser(role: UserRole.Tenant, fullName: "Nguyễn Văn A");
        await _fixture.DbContext.Users.AddAsync(_tenant);
        await _fixture.DbContext.SaveChangesAsync();
    }

    // ── Room status setup ──────────────────────────────────────────────────────

    [Given("the room is in Maintenance status for rental tests")]
    public async Task GivenTheRoomIsInMaintenanceForRentalTests()
    {
        _room.Status = RoomStatus.Maintenance;
        _fixture.DbContext.Rooms.Update(_room);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    // ── Additional actors ──────────────────────────────────────────────────────

    [Given("a second tenant for rental tests")]
    public async Task GivenASecondTenantForRentalTests()
    {
        _tenant2 = TestDataBuilder.CreateUser(role: UserRole.Tenant, fullName: "Trần Thị B");
        await _fixture.DbContext.Users.AddAsync(_tenant2);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given("a second available room \"([^\"]*)\" for rental tests")]
    public async Task GivenASecondAvailableRoomForRentalTests(string roomNumber)
    {
        _room2 = TestDataBuilder.CreateRoom(_building.Id, roomNumber: roomNumber);
        _room2.Status = RoomStatus.Available;
        await _fixture.DbContext.Rooms.AddAsync(_room2);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    // ── Given — pre-existing reservation states ────────────────────────────────

    [Given("a pending reservation with deposit ([0-9]+)")]
    public async Task GivenAPendingReservationWithDeposit(decimal deposit)
    {
        var result = await ReservationHandler().Handle(new CreateReservationCommand
        {
            RoomId        = _room.Id,
            TenantUserId  = _tenant.Id,
            DepositAmount = deposit,
            ExpiresAt     = DateTime.UtcNow.AddDays(7)
        }, default);

        _activeReservationId = result.Id;
        _fixture.DbContext.ChangeTracker.Clear();
    }

    [Given("a confirmed reservation with deposit ([0-9]+)")]
    public async Task GivenAConfirmedReservationWithDeposit(decimal deposit)
    {
        await GivenAPendingReservationWithDeposit(deposit);

        await ReservationStatusHandler().Handle(new ChangeReservationStatusCommand
        {
            Id     = _activeReservationId,
            Action = "CONFIRM"
        }, default);

        _fixture.DbContext.ChangeTracker.Clear();
    }

    [Given("the reservation has been cancelled")]
    public async Task GivenTheReservationHasBeenCancelled()
    {
        await ReservationStatusHandler().Handle(new ChangeReservationStatusCommand
        {
            Id           = _activeReservationId,
            Action       = "CANCEL",
            RefundAmount = 0
        }, default);

        _fixture.DbContext.ChangeTracker.Clear();
    }

    // ── When — direct contract ─────────────────────────────────────────────────

    [When("I create a direct contract with monthly rent ([0-9]+) and deposit ([0-9]+)")]
    public async Task WhenICreateADirectContract(decimal monthlyRent, decimal deposit)
    {
        _lastException = null;
        try
        {
            _contractResult = await ContractHandler().Handle(new CreateContractCommand
            {
                RoomId       = _room.Id,
                TenantUserId = _tenant.Id,
                StartDate    = new DateOnly(2026, 6, 1),
                EndDate      = new DateOnly(2027, 5, 31),
                MoveInDate   = new DateOnly(2026, 6, 1),
                MonthlyRent  = monthlyRent,
                DepositAmount = deposit
            }, default);
            _fixture.DbContext.ChangeTracker.Clear();
        }
        catch (Exception ex)
        {
            _lastException = ex;
        }
    }

    [When("I try to create a direct contract with monthly rent ([0-9]+) and deposit ([0-9]+)")]
    public async Task WhenITryToCreateADirectContract(decimal monthlyRent, decimal deposit)
        => await WhenICreateADirectContract(monthlyRent, deposit);

    [When("I try to create a second direct contract on the same room")]
    public async Task WhenITryToCreateASecondDirectContract()
    {
        _lastException = null;
        try
        {
            await ContractHandler().Handle(new CreateContractCommand
            {
                RoomId        = _room.Id,
                TenantUserId  = _tenant.Id,
                StartDate     = new DateOnly(2026, 7, 1),
                EndDate       = new DateOnly(2027, 6, 30),
                MoveInDate    = new DateOnly(2026, 7, 1),
                MonthlyRent   = 5_000_000,
                DepositAmount = 10_000_000
            }, default);
        }
        catch (Exception ex)
        {
            _lastException = ex;
        }
    }

    // ── When — reservation actions ─────────────────────────────────────────────

    [When("I create a reservation with deposit ([0-9]+)")]
    public async Task WhenICreateAReservationWithDeposit(decimal deposit)
    {
        _lastException = null;
        try
        {
            _reservationResult = await ReservationHandler().Handle(new CreateReservationCommand
            {
                RoomId        = _room.Id,
                TenantUserId  = _tenant.Id,
                DepositAmount = deposit,
                ExpiresAt     = DateTime.UtcNow.AddDays(7)
            }, default);
            _activeReservationId = _reservationResult.Id;
            _fixture.DbContext.ChangeTracker.Clear();
        }
        catch (Exception ex)
        {
            _lastException = ex;
        }
    }

    [When("I confirm the reservation")]
    public async Task WhenIConfirmTheReservation()
    {
        _lastException = null;
        try
        {
            _reservationResult = await ReservationStatusHandler().Handle(
                new ChangeReservationStatusCommand
                {
                    Id     = _activeReservationId,
                    Action = "CONFIRM"
                }, default);
            _fixture.DbContext.ChangeTracker.Clear();
        }
        catch (Exception ex)
        {
            _lastException = ex;
        }
    }

    [When("I cancel the reservation with refund ([0-9]+)")]
    public async Task WhenICancelTheReservationWithRefund(decimal refund)
    {
        _lastException = null;
        try
        {
            _reservationResult = await ReservationStatusHandler().Handle(
                new ChangeReservationStatusCommand
                {
                    Id           = _activeReservationId,
                    Action       = "CANCEL",
                    RefundAmount = refund
                }, default);
            _fixture.DbContext.ChangeTracker.Clear();
        }
        catch (Exception ex)
        {
            _lastException = ex;
        }
    }

    [When("I try to cancel the reservation again with refund ([0-9]+)")]
    public async Task WhenITryCancelAgain(decimal refund)
        => await WhenICancelTheReservationWithRefund(refund);

    [When("I create a contract from the reservation with monthly rent ([0-9]+) and deposit ([0-9]+)")]
    public async Task WhenICreateAContractFromReservation(decimal monthlyRent, decimal deposit)
    {
        _lastException = null;
        try
        {
            _contractResult = await ContractHandler().Handle(new CreateContractCommand
            {
                RoomId        = _room.Id,
                TenantUserId  = _tenant.Id,
                ReservationId = _activeReservationId,
                StartDate     = new DateOnly(2026, 6, 1),
                EndDate       = new DateOnly(2027, 5, 31),
                MoveInDate    = new DateOnly(2026, 6, 1),
                MonthlyRent   = monthlyRent,
                DepositAmount = deposit
            }, default);
            _fixture.DbContext.ChangeTracker.Clear();
        }
        catch (Exception ex)
        {
            _lastException = ex;
        }
    }

    [When("I try to create a contract from the reservation with monthly rent ([0-9]+) and deposit ([0-9]+)")]
    public async Task WhenITryToCreateAContractFromReservation(decimal monthlyRent, decimal deposit)
        => await WhenICreateAContractFromReservation(monthlyRent, deposit);

    // ── When — double-booking attempts ────────────────────────────────────────

    [When("I try to create a reservation for the second tenant")]
    public async Task WhenITryToCreateAReservationForSecondTenant()
    {
        _lastException = null;
        try
        {
            await ReservationHandler().Handle(new CreateReservationCommand
            {
                RoomId        = _room.Id,
                TenantUserId  = _tenant2!.Id,
                DepositAmount = 5_000_000,
                ExpiresAt     = DateTime.UtcNow.AddDays(7)
            }, default);
        }
        catch (Exception ex)
        {
            _lastException = ex;
        }
    }

    [When("I try to create a second reservation on room \"([^\"]*)\"")]
    public async Task WhenITryToCreateASecondReservationOnRoom(string roomNumber)
    {
        var room = roomNumber == _room.RoomNumber ? _room : _room2!;
        _lastException = null;
        try
        {
            await ReservationHandler().Handle(new CreateReservationCommand
            {
                RoomId        = room.Id,
                TenantUserId  = _tenant.Id,
                DepositAmount = 5_000_000,
                ExpiresAt     = DateTime.UtcNow.AddDays(7)
            }, default);
        }
        catch (Exception ex)
        {
            _lastException = ex;
        }
    }

    // ── Then — contract assertions ─────────────────────────────────────────────

    [Then("the contract status should be \"([^\"]*)\"")]
    public void ThenTheContractStatusShouldBe(string status)
    {
        _lastException.Should().BeNull();
        _contractResult.Should().NotBeNull();
        _contractResult!.Status.Should().Be(status);
    }

    [Then("the contract should have ([0-9]+) tenant with IsMainTenant true")]
    public async Task ThenContractShouldHaveMainTenant(int count)
    {
        _lastException.Should().BeNull();
        _contractResult.Should().NotBeNull();

        var tenants = await _fixture.DbContext.ContractTenants
            .Where(ct => ct.ContractId == _contractResult!.Id && ct.IsMainTenant)
            .ToListAsync();

        tenants.Should().HaveCount(count);
    }

    [Then("a contract notification should be created for the tenant")]
    public async Task ThenContractNotificationShouldBeCreated()
    {
        _lastException.Should().BeNull();
        _contractResult.Should().NotBeNull();

        var notification = await _fixture.DbContext.Notifications
            .FirstOrDefaultAsync(n => n.UserId == _tenant.Id
                && n.ReferenceId == _contractResult!.Id);

        notification.Should().NotBeNull("a contract-created notification must be sent to the tenant");
    }

    // ── Then — reservation assertions ──────────────────────────────────────────

    [Then("the reservation status should be \"([^\"]*)\"")]
    public async Task ThenReservationStatusShouldBe(string status)
    {
        _lastException.Should().BeNull();

        var expected = Enum.Parse<ReservationStatus>(status);
        var dbReservation = await _fixture.DbContext.RoomReservations.FindAsync(_activeReservationId);
        dbReservation!.Status.Should().Be(expected);
    }

    [Then("the reservation refund amount should be ([0-9]+)")]
    public async Task ThenReservationRefundAmountShouldBe(decimal amount)
    {
        var dbReservation = await _fixture.DbContext.RoomReservations.FindAsync(_activeReservationId);
        dbReservation!.RefundAmount.Should().Be(amount);
    }

    // ── Then — room state assertions ───────────────────────────────────────────

    [Then("the room status should be \"([^\"]*)\"")]
    public async Task ThenRoomStatusShouldBe(string status)
    {
        var expected = Enum.Parse<RoomStatus>(status);
        var dbRoom = await _fixture.DbContext.Rooms.FindAsync(_room.Id);
        dbRoom!.Status.Should().Be(expected);
    }

    // ── Then — payment assertions ──────────────────────────────────────────────

    [Then("a DEPOSIT_IN payment of ([0-9]+) should be recorded for the contract")]
    public async Task ThenDepositInPaymentForContract(decimal amount)
    {
        _lastException.Should().BeNull();
        _contractResult.Should().NotBeNull();

        var payments = await _fixture.DbContext.Payments
            .Where(p => p.ContractId == _contractResult!.Id && p.Type == PaymentType.DepositIn)
            .ToListAsync();

        payments.Should().Contain(p => p.Amount == amount,
            $"a DEPOSIT_IN payment of {amount} must exist for the contract");
    }

    [Then("a DEPOSIT_IN payment of ([0-9]+) should be recorded for the reservation")]
    public async Task ThenDepositInPaymentForReservation(decimal amount)
    {
        _lastException.Should().BeNull();

        var payment = await _fixture.DbContext.Payments
            .FirstOrDefaultAsync(p => p.ReservationId == _activeReservationId
                && p.Type == PaymentType.DepositIn
                && p.Amount == amount);

        payment.Should().NotBeNull($"a DEPOSIT_IN payment of {amount} must exist for the reservation");
    }

    [Then("a DEPOSIT_REFUND payment of ([0-9]+) should be recorded for the reservation")]
    public async Task ThenDepositRefundPaymentForReservation(decimal amount)
    {
        _lastException.Should().BeNull();

        var payment = await _fixture.DbContext.Payments
            .FirstOrDefaultAsync(p => p.ReservationId == _activeReservationId
                && p.Type == PaymentType.DepositRefund
                && p.Amount == amount);

        payment.Should().NotBeNull($"a DEPOSIT_REFUND payment of {amount} must exist for the reservation");
    }

    [Then("no DEPOSIT_REFUND payment should exist for the reservation")]
    public async Task ThenNoDepositRefundPaymentForReservation()
    {
        var payment = await _fixture.DbContext.Payments
            .FirstOrDefaultAsync(p => p.ReservationId == _activeReservationId
                && p.Type == PaymentType.DepositRefund);

        payment.Should().BeNull("no refund should be recorded when deposit is forfeited");
    }

    [Then("no deposit payments should exist for the reservation")]
    public async Task ThenNoDepositPaymentsForReservation()
    {
        var any = await _fixture.DbContext.Payments
            .AnyAsync(p => p.ReservationId == _activeReservationId);

        any.Should().BeFalse("cancelling a Pending reservation must not create any payment records");
    }

    // ── Then — error assertions ────────────────────────────────────────────────

    [Then("the rental action should be rejected with a conflict error")]
    public void ThenRentalActionRejectedWithConflict()
    {
        _lastException.Should().NotBeNull();
        _lastException.Should().BeOfType<ConflictException>();
    }

    [Then("the rental action should be rejected with a bad request error")]
    public void ThenRentalActionRejectedWithBadRequest()
    {
        _lastException.Should().NotBeNull();
        _lastException.Should().BeOfType<BadRequestException>();
    }
}
