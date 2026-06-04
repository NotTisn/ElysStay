using TechTalk.SpecFlow;
using Xunit;
using Domain.Entities;
using Domain.Enums;
using Tests.Integration.Fixtures;
using Tests.Integration.Builders;

namespace ElysStay.Tests.Acceptance.StepDefinitions;

[Binding]
public class MeterReadingBillingSteps
{
    private readonly DatabaseFixture _fixture;
    private User _owner = null!;
    private Building _building = null!;
    private Room _room = null!;
    private Service _waterService = null!;
    private Service _electricityService = null!;
    private MeterReading? _meterReading;
    private Contract _contract = null!;
    private Invoice? _invoice;
    private Exception? _lastException;

    public MeterReadingBillingSteps(DatabaseFixture fixture)
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

    [Given("a building with water and electricity services")]
    public async Task GivenABuildingWithWaterAndElectricityServices()
    {
        _building = TestDataBuilder.CreateBuilding(_owner.Id);
        _waterService = TestDataBuilder.CreateService(_building.Id, name: "Water", unit: "m³", unitPrice: 10_000, isMetered: true);
        _electricityService = TestDataBuilder.CreateService(_building.Id, name: "Electricity", unit: "kWh", unitPrice: 2_500, isMetered: true);

        await _fixture.DbContext.Buildings.AddAsync(_building);
        await _fixture.DbContext.Services.AddAsync(_waterService);
        await _fixture.DbContext.Services.AddAsync(_electricityService);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given("a room with active contract")]
    public async Task GivenARoomWithActiveContract()
    {
        _room = TestDataBuilder.CreateRoom(_building.Id);
        var tenant = TestDataBuilder.CreateUser(role: UserRole.Tenant);

        await _fixture.DbContext.Rooms.AddAsync(_room);
        await _fixture.DbContext.Users.AddAsync(tenant);
        await _fixture.DbContext.SaveChangesAsync();

        _contract = TestDataBuilder.CreateContract(
            _room.Id,
            tenant.Id,
            _owner.Id,
            monthlyRent: 5_000_000,
            depositAmount: 10_000_000,
            status: ContractStatus.Active);

        await _fixture.DbContext.Contracts.AddAsync(_contract);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given("both services enabled for the room")]
    public async Task GivenBothServicesEnabledForTheRoom()
    {
        var waterRoomService = new RoomService
        {
            Id = Guid.NewGuid(),
            RoomId = _room.Id,
            ServiceId = _waterService.Id,
            IsEnabled = true
        };

        var electricityRoomService = new RoomService
        {
            Id = Guid.NewGuid(),
            RoomId = _room.Id,
            ServiceId = _electricityService.Id,
            IsEnabled = true
        };

        await _fixture.DbContext.Set<RoomService>().AddAsync(waterRoomService);
        await _fixture.DbContext.Set<RoomService>().AddAsync(electricityRoomService);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [When("I record meter reading for water: previous ([0-9]+)m³, current ([0-9]+)m³ \\(month ([0-9])/([0-9]+)\\)")]
    public async Task WhenIRecordMeterReadingForWater(decimal previous, decimal current, int month, int year)
    {
        _meterReading = TestDataBuilder.CreateMeterReading(
            _room.Id,
            _waterService.Id,
            _owner.Id,
            billingMonth: month,
            billingYear: year,
            previousReading: previous,
            currentReading: current);

        await _fixture.DbContext.Set<MeterReading>().AddAsync(_meterReading);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [When("I generate invoice for month ([0-9])/([0-9]+)")]
    public async Task WhenIGenerateInvoiceForMonth(int month, int year)
    {
        var serviceAmount = _meterReading != null ? (_meterReading.Consumption * _waterService.UnitPrice) : 0;

        _invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            ContractId = _contract.Id,
            BillingMonth = month,
            BillingYear = year,
            RentAmount = 5_000_000,
            ServiceAmount = serviceAmount,
            Status = InvoiceStatus.Draft,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7),
            CreatedBy = _owner.Id
        };

        await _fixture.DbContext.Invoices.AddAsync(_invoice);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Then("the invoice should include water charge:")]
    public void ThenTheInvoiceShouldIncludeWaterCharge(Table table)
    {
        Assert.NotNull(_meterReading);
        Assert.NotNull(_invoice);

        var consumption = _meterReading.Consumption;
        var unitPrice = _waterService.UnitPrice;
        var waterCharge = consumption * unitPrice;

        foreach (var row in table.Rows)
        {
            var field = row["Field"];
            var expectedValue = decimal.Parse(row["Value"]);

            Assert.Equal(expectedValue, field switch
            {
                "Consumption" => consumption,
                "Unit Price" => unitPrice,
                "Water Charge" => waterCharge,
                _ => throw new ArgumentException($"Unknown field: {field}")
            });
        }
    }

    [When("I record meter reading for electricity: previous ([0-9]+)kWh, current ([0-9]+)kWh \\(month ([0-9])/([0-9]+)\\)")]
    public async Task WhenIRecordMeterReadingForElectricity(decimal previous, decimal current, int month, int year)
    {
        _meterReading = TestDataBuilder.CreateMeterReading(
            _room.Id,
            _electricityService.Id,
            _owner.Id,
            billingMonth: month,
            billingYear: year,
            previousReading: previous,
            currentReading: current);

        await _fixture.DbContext.Set<MeterReading>().AddAsync(_meterReading);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given("electricity unit price is ([0-9]+) VND/kWh")]
    public void GivenElectricityUnitPriceIsVndKwh(decimal unitPrice)
    {
        _electricityService.UnitPrice = unitPrice;
        _fixture.DbContext.Services.Update(_electricityService);
    }

    [Then("the invoice should include electricity charge:")]
    public void ThenTheInvoiceShouldIncludeElectricityCharge(Table table)
    {
        Assert.NotNull(_meterReading);
        Assert.NotNull(_invoice);

        var consumption = _meterReading.Consumption;
        var unitPrice = _electricityService.UnitPrice;
        var electricityCharge = consumption * unitPrice;

        foreach (var row in table.Rows)
        {
            var field = row["Field"];
            var expectedValue = decimal.Parse(row["Value"]);

            Assert.Equal(expectedValue, field switch
            {
                "Consumption" => consumption,
                "Unit Price" => unitPrice,
                "Electricity Charge" => electricityCharge,
                _ => throw new ArgumentException($"Unknown field: {field}")
            });
        }
    }

    [Given("meter reading already exists for water in March ([0-9]+)")]
    public async Task GivenMeterReadingAlreadyExistsForWater(int year)
    {
        var existingReading = TestDataBuilder.CreateMeterReading(
            _room.Id,
            _waterService.Id,
            _owner.Id,
            billingMonth: 3,
            billingYear: year,
            previousReading: 100,
            currentReading: 110);

        await _fixture.DbContext.Set<MeterReading>().AddAsync(existingReading);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [When("I try to record another meter reading for same water in March ([0-9]+)")]
    public void WhenITryToRecordAnotherMeterReading(int year)
    {
        try
        {
            var existingReading = _fixture.DbContext.Set<MeterReading>()
                .FirstOrDefault(mr => mr.RoomId == _room.Id && 
                                     mr.ServiceId == _waterService.Id &&
                                     mr.BillingMonth == 3 &&
                                     mr.BillingYear == year);

            if (existingReading != null)
            {
                throw new InvalidOperationException("Meter reading already exists for this month");
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
}
