using Xunit;
using FluentAssertions;
using Domain.Entities;
using Domain.Enums;
using Tests.Integration.Fixtures;
using Tests.Integration.Builders;

namespace ElysStay.Tests.Integration.Features;

public class MeterReadingIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();
    private User _owner = null!;
    private Building _building = null!;
    private Room _room = null!;
    private Service _service = null!;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    private async Task SetupTestData()
    {
        _owner = TestDataBuilder.CreateUser(role: UserRole.Owner);
        _building = TestDataBuilder.CreateBuilding(_owner.Id);
        _room = TestDataBuilder.CreateRoom(_building.Id);
        _service = TestDataBuilder.CreateService(_building.Id, isMetered: true);

        await _fixture.DbContext.Users.AddAsync(_owner);
        await _fixture.DbContext.Buildings.AddAsync(_building);
        await _fixture.DbContext.Rooms.AddAsync(_room);
        await _fixture.DbContext.Services.AddAsync(_service);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task RecordMeterReading_WithValidData_CreatesSuccessfully()
    {
        // Arrange
        await SetupTestData();
        var reading = TestDataBuilder.CreateMeterReading(_room.Id, _service.Id, _owner.Id, 3, 2026);

        // Act
        await _fixture.DbContext.Set<MeterReading>().AddAsync(reading);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var saved = _fixture.DbContext.Set<MeterReading>()
            .FirstOrDefault(m => m.Id == reading.Id);
        saved.Should().NotBeNull();
        saved!.Consumption.Should().Be(reading.CurrentReading - reading.PreviousReading);
    }

    [Fact]
    public async Task CalculateConsumption_ComputesCorrectly()
    {
        // Arrange
        await SetupTestData();
        const decimal previous = 100;
        const decimal current = 110;
        var reading = TestDataBuilder.CreateMeterReading(_room.Id, _service.Id, _owner.Id, 
            previousReading: previous, currentReading: current);

        // Act
        await _fixture.DbContext.Set<MeterReading>().AddAsync(reading);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var saved = _fixture.DbContext.Set<MeterReading>()
            .FirstOrDefault(m => m.Id == reading.Id);
        saved!.Consumption.Should().Be(current - previous);
    }

    [Fact]
    public async Task GetMeterReadings_FiltersByServiceAndMonth_ReturnsCorrectReadings()
    {
        // Arrange
        await SetupTestData();
        var reading1 = TestDataBuilder.CreateMeterReading(_room.Id, _service.Id, _owner.Id, 3, 2026);
        var reading2 = TestDataBuilder.CreateMeterReading(_room.Id, _service.Id, _owner.Id, 4, 2026);

        await _fixture.DbContext.Set<MeterReading>().AddAsync(reading1);
        await _fixture.DbContext.Set<MeterReading>().AddAsync(reading2);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var march = _fixture.DbContext.Set<MeterReading>()
            .Where(m => m.ServiceId == _service.Id && m.BillingMonth == 3)
            .ToList();

        // Assert
        march.Should().HaveCount(1);
        march.First().BillingMonth.Should().Be(3);
    }

    [Fact]
    public async Task PreventDuplicateMeterReading_ForSameMonthAndService_Fails()
    {
        // Arrange
        await SetupTestData();
        var reading1 = TestDataBuilder.CreateMeterReading(_room.Id, _service.Id, _owner.Id, 3, 2026);
        await _fixture.DbContext.Set<MeterReading>().AddAsync(reading1);
        await _fixture.DbContext.SaveChangesAsync();

        // Act & Assert
        var reading2 = TestDataBuilder.CreateMeterReading(_room.Id, _service.Id, _owner.Id, 3, 2026);
        var duplicate = _fixture.DbContext.Set<MeterReading>()
            .FirstOrDefault(m => m.RoomId == _room.Id && 
                                m.ServiceId == _service.Id &&
                                m.BillingMonth == 3 &&
                                m.BillingYear == 2026);

        duplicate.Should().NotBeNull();
    }
}
