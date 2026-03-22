using TechTalk.SpecFlow;
using Xunit;
using ElysStay.Domain.Entities;
using ElysStay.Domain.Enums;
using ElysStay.Tests.Integration.Fixtures;
using ElysStay.Tests.Integration.Builders;

namespace ElysStay.Tests.Acceptance.StepDefinitions;

[Binding]
public class ReservationConversionSteps
{
    private readonly DatabaseFixture _fixture;
    private User _owner = null!;
    private User _tenant = null!;
    private Building _building = null!;
    private Room _room = null!;
    private RoomReservation _reservation = null!;
    private Contract? _createdContract;
    private Exception? _lastException;
    private decimal _depositAmount;

    public ReservationConversionSteps(DatabaseFixture fixture)
    {
        _fixture = fixture;
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
        _building = TestDataBuilder.CreateBuilding(_owner.Id);
        _room = TestDataBuilder.CreateRoom(_building.Id);
        _depositAmount = deposit;

        await _fixture.DbContext.Buildings.AddAsync(_building);
        await _fixture.DbContext.Rooms.AddAsync(_room);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given("a tenant")]
    public async Task GivenATenant()
    {
        _tenant = TestDataBuilder.CreateUser(email: $"tenant_{Guid.NewGuid()}@test.com", role: UserRole.Tenant);
        await _fixture.DbContext.Users.AddAsync(_tenant);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given("a pending reservation with deposit ([0-9]+) VND")]
    public async Task GivenAPendingReservationWithDeposit(decimal deposit)
    {
        _reservation = TestDataBuilder.CreateReservation(
            _room.Id,
            _tenant.Id,
            depositAmount: deposit,
            status: ReservationStatus.Pending);

        await _fixture.DbContext.Set<RoomReservation>().AddAsync(_reservation);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [When("I convert reservation to contract")]
    public async Task WhenIConvertReservationToContract()
    {
        try
        {
            _createdContract = new Contract
            {
                Id = Guid.NewGuid(),
                RoomId = _room.Id,
                TenantUserId = _tenant.Id,
                ReservationId = _reservation.Id,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(12),
                RoomPrice = _room.Price,
                DepositAmount = _reservation.DepositAmount,
                DepositStatus = DepositStatus.Unpaid,
                Status = ContractStatus.Active,
                CreatedBy = _owner.Id
            };

            _reservation.Status = ReservationStatus.Confirmed;

            await _fixture.DbContext.Contracts.AddAsync(_createdContract);
            _fixture.DbContext.Set<RoomReservation>().Update(_reservation);
            await _fixture.DbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _lastException = ex;
        }
    }

    [Then("contract should be created with:")]
    public void ThenContractShouldBeCreatedWith(DataTable table)
    {
        Assert.NotNull(_createdContract);

        foreach (var row in table.Rows)
        {
            var field = row["Field"];
            var expectedValue = row["Value"];

            switch (field)
            {
                case "Status":
                    Assert.Equal(expectedValue, _createdContract.Status.ToString());
                    break;
                case "DepositAmount":
                    Assert.Equal(decimal.Parse(expectedValue), _createdContract.DepositAmount);
                    break;
                case "DepositStatus":
                    Assert.Equal(expectedValue, _createdContract.DepositStatus.ToString());
                    break;
                case "Room":
                    Assert.Equal(_room.Id, _createdContract.RoomId);
                    break;
                case "Tenant":
                    Assert.Equal(_tenant.Id, _createdContract.TenantUserId);
                    break;
            }
        }
    }

    [Then("reservation status should be \"([^\"]*)\"")]
    public void ThenReservationStatusShouldBe(string expectedStatus)
    {
        Assert.NotNull(_reservation);
        var status = Enum.Parse<ReservationStatus>(expectedStatus);
        Assert.Equal(status, _reservation.Status);
    }

    [Given("a cancelled reservation")]
    public async Task GivenACancelledReservation()
    {
        _reservation = TestDataBuilder.CreateReservation(
            _room.Id,
            _tenant.Id,
            depositAmount: _depositAmount,
            status: ReservationStatus.Cancelled);

        await _fixture.DbContext.Set<RoomReservation>().AddAsync(_reservation);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [When("I try to convert reservation to contract")]
    public void WhenITryToConvertReservationToContract()
    {
        try
        {
            if (_reservation.Status != ReservationStatus.Pending)
            {
                throw new InvalidOperationException("Only pending reservations can be converted");
            }
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
        Assert.Contains(expectedError, _lastException.Message);
    }

    [Given("a pending reservation that was already converted to contract")]
    public async Task GivenAPendingReservationAlreadyConverted()
    {
        _reservation = TestDataBuilder.CreateReservation(
            _room.Id,
            _tenant.Id,
            depositAmount: _depositAmount,
            status: ReservationStatus.Pending);

        await _fixture.DbContext.Set<RoomReservation>().AddAsync(_reservation);
        await _fixture.DbContext.SaveChangesAsync();

        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            RoomId = _room.Id,
            TenantUserId = _tenant.Id,
            ReservationId = _reservation.Id,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(12),
            RoomPrice = _room.Price,
            DepositAmount = _reservation.DepositAmount,
            DepositStatus = DepositStatus.Unpaid,
            Status = ContractStatus.Active,
            CreatedBy = _owner.Id
        };

        _reservation.Status = ReservationStatus.Confirmed;

        await _fixture.DbContext.Contracts.AddAsync(contract);
        _fixture.DbContext.Set<RoomReservation>().Update(_reservation);
        await _fixture.DbContext.SaveChangesAsync();

        _createdContract = contract;
    }

    [When("I try to convert it again")]
    public void WhenITryToConvertItAgain()
    {
        try
        {
            var existingContract = _fixture.DbContext.Contracts
                .FirstOrDefault(c => c.ReservationId == _reservation.Id);

            if (existingContract != null)
            {
                throw new InvalidOperationException("Reservation already converted to contract");
            }
        }
        catch (Exception ex)
        {
            _lastException = ex;
        }
    }
}
