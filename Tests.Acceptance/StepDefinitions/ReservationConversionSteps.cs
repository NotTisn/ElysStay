using Application.Common.Exceptions;
using Application.Features.Contracts.Commands;
using Application.Features.Contracts.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TechTalk.SpecFlow;
using TechTalk.SpecFlow.Assist;
using Xunit;
using Tests.Integration.Builders;
using Tests.Integration.Fixtures;

namespace ElysStay.Tests.Acceptance.StepDefinitions;

[Binding]
public class ReservationConversionSteps
{
    private readonly DatabaseFixture _fixture;
    private readonly IMediator _mediator;

    private User _owner = null!;
    private User _tenant = null!;
    private Building _building = null!;
    private Room _room = null!;
    private RoomReservation _reservation = null!;

    private ContractDto? _createdContract;
    private Exception? _lastException;

    private decimal _depositAmount;

    public ReservationConversionSteps(
        DatabaseFixture fixture,
        IMediator mediator)
    {
        _fixture = fixture;
        _mediator = mediator;
    }

    [Given("a building owner")]
    public async Task GivenABuildingOwner()
    {
        _owner = TestDataBuilder.CreateUser(role: UserRole.Manager);

        await _fixture.DbContext.Users.AddAsync(_owner);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given("a room with deposit required ([0-9]+) VND")]
    public async Task GivenARoomWithDepositRequired(decimal deposit)
    {
        _depositAmount = deposit;

        _building = TestDataBuilder.CreateBuilding(_owner.Id);

        _room = TestDataBuilder.CreateRoom(_building.Id);
        _room.Status = RoomStatus.Reserved;

        await _fixture.DbContext.Buildings.AddAsync(_building);
        await _fixture.DbContext.Rooms.AddAsync(_room);

        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given("a tenant")]
    public async Task GivenATenant()
    {
        _tenant = TestDataBuilder.CreateUser(
            email: $"tenant_{Guid.NewGuid()}@test.com",
            role: UserRole.Tenant);

        _tenant.Status = UserStatus.Active;

        await _fixture.DbContext.Users.AddAsync(_tenant);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given("a confirmed reservation with deposit ([0-9]+) VND")]
    public async Task GivenAConfirmedReservationWithDeposit(decimal deposit)
    {
        _reservation = TestDataBuilder.CreateReservation(
            _room.Id,
            _tenant.Id,
            depositAmount: deposit,
            status: ReservationStatus.Confirmed);

        _reservation.ExpiresAt = DateTime.UtcNow.AddDays(1);

        await _fixture.DbContext.Set<RoomReservation>()
            .AddAsync(_reservation);

        await _fixture.DbContext.SaveChangesAsync();
    }

    [When("I convert reservation to contract")]
    public async Task WhenIConvertReservationToContract()
    {
        try
        {
            var command = new CreateContractCommand
            {
                RoomId = _room.Id,
                TenantUserId = _tenant.Id,
                ReservationId = _reservation.Id,

                StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(12)),
                MoveInDate = DateOnly.FromDateTime(DateTime.UtcNow),

                MonthlyRent = 5_000_000,
                DepositAmount = _reservation.DepositAmount,

                Note = "Acceptance test"
            };

            _createdContract = await _mediator.Send(command);
        }
        catch (Exception ex)
        {
            _lastException = ex;
        }
    }

    [Then("contract should be created with:")]
    public void ThenContractShouldBeCreatedWith(Table table)
    {
        Assert.NotNull(_createdContract);

        foreach (var row in table.Rows)
        {
            var field = row["Field"];
            var expectedValue = row["Value"];

            switch (field)
            {
                case "Status":
                    Assert.Equal(expectedValue, _createdContract!.Status);
                    break;

                case "DepositAmount":
                    Assert.Equal(
                        decimal.Parse(expectedValue),
                        _createdContract!.DepositAmount);
                    break;

                case "DepositStatus":
                    Assert.Equal(
                        expectedValue,
                        _createdContract!.DepositStatus);
                    break;

                case "Room":
                    Assert.Equal(
                        _room.Id,
                        _createdContract!.RoomId);
                    break;

                case "Tenant":
                    Assert.Equal(
                        _tenant.Id,
                        _createdContract!.TenantUserId);
                    break;
            }
        }
    }

    [Then("reservation status should be \"([^\"]*)\"")]
    public async Task ThenReservationStatusShouldBe(string expectedStatus)
    {
        var reservation = await _fixture.DbContext
            .Set<RoomReservation>()
            .FirstAsync(r => r.Id == _reservation.Id);

        var expected = Enum.Parse<ReservationStatus>(expectedStatus);

        Assert.Equal(expected, reservation.Status);
    }

    [Given("a cancelled reservation")]
    public async Task GivenACancelledReservation()
    {
        _reservation = TestDataBuilder.CreateReservation(
            _room.Id,
            _tenant.Id,
            depositAmount: _depositAmount,
            status: ReservationStatus.Cancelled);

        await _fixture.DbContext.Set<RoomReservation>()
            .AddAsync(_reservation);

        await _fixture.DbContext.SaveChangesAsync();
    }

    [When("I try to convert reservation to contract")]
    public async Task WhenITryToConvertReservationToContract()
    {
        try
        {
            var command = new CreateContractCommand
            {
                RoomId = _room.Id,
                TenantUserId = _tenant.Id,
                ReservationId = _reservation.Id,

                StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(12)),
                MoveInDate = DateOnly.FromDateTime(DateTime.UtcNow),

                MonthlyRent = 5_000_000,
                DepositAmount = _depositAmount
            };

            await _mediator.Send(command);
        }
        catch (Exception ex)
        {
            _lastException = ex;
        }
    }

    [Then("system should reject with error \"([^\"]*)\"")]
    public void ThenSystemShouldRejectWithError(string expectedError)
    {
        Assert.NotNull(_lastException);

        Assert.Contains(expectedError, _lastException!.Message);
    }
}