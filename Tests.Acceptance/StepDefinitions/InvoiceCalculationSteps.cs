// using TechTalk.SpecFlow;
// using Xunit;
// using Domain.Entities;
// using Domain.Enums;
// using Tests.Integration.Fixtures;
// using Tests.Integration.Builders;
// using Microsoft.EntityFrameworkCore;

// namespace ElysStay.Tests.Acceptance.StepDefinitions;

// [Binding]
// public class InvoiceCalculationSteps
// {
//     private readonly DatabaseFixture _fixture;
//     private User _owner = null!;
//     private Building _building = null!;
//     private Room _room = null!;
//     private User _tenant = null!;
//     private Contract _contract = null!;
//     private Invoice? _generatedInvoice;
//     private List<Invoice> _invoices = new();
//     private List<Service> _services = new();
//     private Dictionary<Guid, MeterReading> _meterReadings = new();
//     private Dictionary<Guid, int> _roomOccupants = new();
//     private Dictionary<string, object> _serviceOverrides = new();
//     private Exception? _lastException;
//     private List<string> _warnings = new();
//     private int _activeDays = 31;

//     public InvoiceCalculationSteps(DatabaseFixture fixture)
//     {
//         _fixture = fixture;
//     }

//     [Given("a building owner with email \"([^\"]*)\"")]
//     public async Task GivenABuildingOwnerWithEmail(string email)
//     {
//         _owner = TestDataBuilder.CreateUser(email: email, role: UserRole.Manager);
//         await _fixture.DbContext.Users.AddAsync(_owner);
//         await _fixture.DbContext.SaveChangesAsync();
//     }

//     [Given("a building named \"([^\"]*)\" owned by the owner")]
//     public async Task GivenABuildingNamedOwnedByTheOwner(string buildingName)
//     {
//         _building = TestDataBuilder.CreateBuilding(_owner.Id, name: buildingName);
//         await _fixture.DbContext.Buildings.AddAsync(_building);
//         await _fixture.DbContext.SaveChangesAsync();
//     }

//     [Given("a room \"([^\"]*)\" in the building with rent ([0-9]+) VND per month")]
//     public async Task GivenARoomInTheBuildingWithRent(string roomNumber, decimal rent)
//     {
//         _room = TestDataBuilder.CreateRoom(_building.Id, roomNumber: roomNumber, price: rent);
//         await _fixture.DbContext.Rooms.AddAsync(_room);
//         await _fixture.DbContext.SaveChangesAsync();
//     }

//     [Given("a tenant with email \"([^\"]*)\"")]
//     public async Task GivenATenantWithEmail(string email)
//     {
//         _tenant = TestDataBuilder.CreateUser(email: email, role: UserRole.Tenant);
//         await _fixture.DbContext.Users.AddAsync(_tenant);
//         await _fixture.DbContext.SaveChangesAsync();
//     }

//     [Given("an active contract between tenant and room \"([^\"]*)\"")]
//     public async Task GivenAnActiveContractBetweenTenantAndRoom(string roomNumber)
//     {
//        _contract = TestDataBuilder.CreateContract(
//         _room.Id,
//         _tenant.Id,
//         _owner.Id,
//         monthlyRent: _room.Price,
//         depositAmount: _room.Price,
//         status: ContractStatus.Active);

//         await _fixture.DbContext.Contracts.AddAsync(_contract);
//         await _fixture.DbContext.SaveChangesAsync();
//     }

//     [Given("water service enabled with unit price ([0-9]+) VND/m³")]
//     public async Task GivenWaterServiceEnabledWithUnitPrice(decimal unitPrice)
//     {
//         _waterService = TestDataBuilder.CreateService(_building.Id, name: "Water", unit: "m³", unitPrice: unitPrice, isMetered: true);
//         await _fixture.DbContext.Services.AddAsync(_waterService);
//         await _fixture.DbContext.SaveChangesAsync();

//         var roomService = new RoomService
//         {
//             Id = Guid.NewGuid(),
//             RoomId = _room.Id,
//             ServiceId = _waterService.Id,
//             IsEnabled = true
//         };
//         await _fixture.DbContext.Set<RoomService>().AddAsync(roomService);
//         await _fixture.DbContext.SaveChangesAsync();
//     }

//     [Given("meter reading for room \"([^\"]*)\": previous ([0-9]+)m³, current ([0-9]+)m³")]
//     public async Task GivenMeterReadingForRoom(string roomNumber, decimal previousReading, decimal currentReading)
//     {
//         Assert.NotNull(_waterService);
//         _meterReading = TestDataBuilder.CreateMeterReading(
//             _room.Id,
//             _waterService.Id,
//             _owner.Id,
//             billingMonth: 3,
//             billingYear: 2026,
//             previousReading: previousReading,
//             currentReading: currentReading);

//         await _fixture.DbContext.Set<MeterReading>().AddAsync(_meterReading);
//         await _fixture.DbContext.SaveChangesAsync();
//     }

//     [When("I generate invoice for ([A-Za-z]+) ([0-9]+)")]
//     public async Task WhenIGenerateInvoice(string monthName, int year)
//     {
//         var monthMap = new Dictionary<string, int>
//         {
//             { "March", 3 },
//             { "April", 4 },
//             { "May", 5 }
//         };

//         int month = monthMap[monthName];

//         var invoice = new Invoice
//         {
//             Id = Guid.NewGuid(),
//             ContractId = _contract.Id,
//             BillingMonth = month,
//             BillingYear = year,
//             RentAmount = _room.Price,
//             ServiceAmount = _meterReading != null ? (_meterReading.Consumption * _waterService!.UnitPrice) : 0,
//             PenaltyAmount = _penaltyAmount,
//             DiscountAmount = _discountAmount,
//             Status = InvoiceStatus.Unpaid,
//             DueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7),
//             CreatedBy = _owner.Id
//         };

//         _generatedInvoice = invoice;
//         await _fixture.DbContext.Invoices.AddAsync(_generatedInvoice);
//         await _fixture.DbContext.SaveChangesAsync();
//     }

//     [When("I generate invoice for ([A-Za-z]+) ([0-9]+) with penalty ([0-9]+) and discount ([0-9]+)")]
//     public async Task WhenIGenerateInvoiceWithPenaltyAndDiscount(string monthName, int year, decimal penalty, decimal discount)
//     {
//         _penaltyAmount = penalty;
//         _discountAmount = discount;
//         await WhenIGenerateInvoice(monthName, year);
//     }

//     [Then("the invoice should have:")]
//     public void ThenTheInvoiceShouldHave(Table table)
//     {
//         Assert.NotNull(_generatedInvoice);

//         foreach (var row in table.Rows)
//         {
//             string field = row["Field"];
//             decimal expectedValue = decimal.Parse(row["Value"]);

//             Assert.Equal(expectedValue, field switch
//             {
//                 "RoomAmount" => _generatedInvoice.RentAmount,
//                 "ServiceAmount" => _generatedInvoice.ServiceAmount,
//                 "PenaltyAmount" => _generatedInvoice.PenaltyAmount,
//                 "DiscountAmount" => _generatedInvoice.DiscountAmount,
//                 "TotalAmount" => _generatedInvoice.RentAmount + _generatedInvoice.ServiceAmount 
//                                   + _generatedInvoice.PenaltyAmount - _generatedInvoice.DiscountAmount,
//                 _ => throw new ArgumentException($"Unknown field: {field}")
//             });
//         }
//     }

//     [Then("invoice status should be \"([^\"]*)\"")]
//     public void ThenInvoiceStatusShouldBe(string status)
//     {
//         Assert.NotNull(_generatedInvoice);
//         var expectedStatus = Enum.Parse<InvoiceStatus>(status);
//         Assert.Equal(expectedStatus, _generatedInvoice.Status);
//     }

//     [Then("the invoice should include water charge:")]
//     public void ThenTheInvoiceShouldIncludeWaterCharge(Table table)
//     {
//         Assert.NotNull(_meterReading);
//         Assert.NotNull(_waterService);
//         Assert.NotNull(_generatedInvoice);

//         var consumption = _meterReading.Consumption;
//         var unitPrice = _waterService.UnitPrice;
//         var expectedCharge = consumption * unitPrice;

//         foreach (var row in table.Rows)
//         {
//             string field = row["Field"];
//             decimal expectedValue = decimal.Parse(row["Value"]);

//             Assert.Equal(expectedValue, field switch
//             {
//                 "Consumption" => consumption,
//                 "Unit Price" => unitPrice,
//                 "Water Charge" => expectedCharge,
//                 _ => throw new ArgumentException($"Unknown field: {field}")
//             });
//         }
//     }

//     [Then("the invoice should include electricity charge:")]
//     public void ThenTheInvoiceShouldIncludeElectricityCharge(Table table)
//     {
//         // Similar structure for electricity
//         foreach (var row in table.Rows)
//         {
//             // Would implement similar assertions
//         }
//     }
// }
