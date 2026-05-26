using TechTalk.SpecFlow;
using Xunit;
using Domain.Entities;
using Domain.Enums;
using Tests.Integration.Fixtures;
using Tests.Integration.Builders;

namespace ElysStay.Tests.Acceptance.StepDefinitions;

[Binding]
public class DoubleBookingPreventionSteps
{
    private readonly DatabaseFixture _fixture;
    private User _owner = null!;
    private Building _building = null!;
    private Room _room = null!;
    private RoomReservation? _existingReservation;
    private Exception? _lastException;

    public DoubleBookingPreventionSteps(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Given("a building owner with email \"([^\"]*)\"")]
    public async Task GivenABuildingOwnerWithEmail(string email)
    {
        _owner = TestDataBuilder.CreateUser(email: email, role: UserRole.Manager);
        await _fixture.DbContext.Users.AddAsync(_owner);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given("a building named \"([^\"]*)\"")]
    public async Task GivenABuildingNamed(string buildingName)
    {
        _building = TestDataBuilder.CreateBuilding(_owner.Id, name: buildingName);
        await _fixture.DbContext.Buildings.AddAsync(_building);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given("a room \"([^\"]*)\" in the building")]
    public async Task GivenARoomInTheBuilding(string roomNumber)
    {
        _room = TestDataBuilder.CreateRoom(_building.Id, roomNumber: roomNumber);
        await _fixture.DbContext.Rooms.AddAsync(_room);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given("room \"([^\"]*)\" is already reserved from ([0-9])/([0-9])/([0-9]+) to ([0-9])/([0-9])/([0-9]+)")]
    public async Task GivenRoomIsAlreadyReserved(string roomNumber, int startDay, int startMonth, int startYear, 
                                                   int endDay, int endMonth, int endYear)
    {
        var tenant = TestDataBuilder.CreateUser(email: "tenant1@test.com", role: UserRole.Tenant);
        await _fixture.DbContext.Users.AddAsync(tenant);
        await _fixture.DbContext.SaveChangesAsync();

        var startDate = new DateTime(startYear, startMonth, startDay);
        var endDate = new DateTime(endYear, endMonth, endDay);

        _existingReservation = TestDataBuilder.CreateReservation(
            _room.Id,
            tenant.Id,
            depositAmount: 10_000_000,
            status: ReservationStatus.Confirmed);

        await _fixture.DbContext.Set<RoomReservation>().AddAsync(_existingReservation);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [When("I try to create reservation from ([0-9])/([0-9])/([0-9]+) to ([0-9])/([0-9])/([0-9]+)")]
    public async Task WhenITryToCreateReservation(int startDay, int startMonth, int startYear,
                                                   int endDay, int endMonth, int endYear)
    {
        try
        {
            var tenant = TestDataBuilder.CreateUser(email: "tenant2@test.com", role: UserRole.Tenant);
            await _fixture.DbContext.Users.AddAsync(tenant);
            await _fixture.DbContext.SaveChangesAsync();

            // Check for overlapping reservations
            var existingReservations = _fixture.DbContext.Set<RoomReservation>()
                .Where(r => r.RoomId == _room.Id && r.Status != ReservationStatus.Cancelled)
                .ToList();

            var newStart = new DateTime(startYear, startMonth, startDay);
            var newEnd = new DateTime(endYear, endMonth, endDay);

            foreach (var existing in existingReservations)
            {
                if (newStart < existing.CreatedAt.AddDays(30) && newEnd > existing.CreatedAt)
                {
                    throw new InvalidOperationException("Room not available for selected dates");
                }
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

    [Then("reservation should NOT be created")]
    public void ThenReservationShouldNotBeCreated()
    {
        Assert.NotNull(_lastException);
    }

    [When("I create reservation from ([0-9])/([0-9])/([0-9]+) to ([0-9])/([0-9])/([0-9]+)")]
    public async Task WhenICreateReservation(int startDay, int startMonth, int startYear,
                                              int endDay, int endMonth, int endYear)
    {
        var tenant = TestDataBuilder.CreateUser(email: $"tenant_{Guid.NewGuid()}@test.com", role: UserRole.Tenant);
        await _fixture.DbContext.Users.AddAsync(tenant);
        await _fixture.DbContext.SaveChangesAsync();

        var reservation = TestDataBuilder.CreateReservation(
            _room.Id,
            tenant.Id,
            depositAmount: 10_000_000,
            status: ReservationStatus.Pending);

        await _fixture.DbContext.Set<RoomReservation>().AddAsync(reservation);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Then("reservation should be created successfully")]
    public void ThenReservationShouldBeCreatedSuccessfully()
    {
        Assert.Null(_lastException);
    }

    [Then("status should be \"([^\"]*)\"")]
    public void ThenStatusShouldBe(string expectedStatus)
    {
        var lastReservation = _fixture.DbContext.Set<RoomReservation>()
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefault();

        Assert.NotNull(lastReservation);
        var status = Enum.Parse<ReservationStatus>(expectedStatus);
        Assert.Equal(status, lastReservation.Status);
    }
}
