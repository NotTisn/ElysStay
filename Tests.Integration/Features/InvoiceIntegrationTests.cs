using Xunit;
using FluentAssertions;
using Domain.Entities;
using Domain.Enums;
using Tests.Integration.Fixtures;
using Tests.Integration.Builders;

namespace ElysStay.Tests.Integration.Features;

public class InvoiceIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();
    private User _owner = null!;
    private Building _building = null!;
    private Room _room = null!;
    private User _tenant = null!;
    private Contract _contract = null!;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    private async Task SetupTestData()
    {
        _owner = TestDataBuilder.CreateUser(role: UserRole.Owner);
        _tenant = TestDataBuilder.CreateUser(role: UserRole.Tenant);
        _building = TestDataBuilder.CreateBuilding(_owner.Id);
        _room = TestDataBuilder.CreateRoom(_building.Id);
        _contract = TestDataBuilder.CreateContract(_room.Id, _tenant.Id, _owner.Id);

        await _fixture.DbContext.Users.AddAsync(_owner);
        await _fixture.DbContext.Users.AddAsync(_tenant);
        await _fixture.DbContext.Buildings.AddAsync(_building);
        await _fixture.DbContext.Rooms.AddAsync(_room);
        await _fixture.DbContext.Contracts.AddAsync(_contract);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task GenerateInvoice_WithValidContract_CreatesInvoiceSuccessfully()
    {
        // Arrange
        await SetupTestData();
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            ContractId = _contract.Id,
            BillingMonth = 3,
            BillingYear = 2026,
            RentAmount = _contract.MonthlyRent,
            Status = InvoiceStatus.Draft,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            CreatedBy = _owner.Id
        };

        // Act
        await _fixture.DbContext.Invoices.AddAsync(invoice);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var savedInvoice = _fixture.DbContext.Invoices.FirstOrDefault(i => i.Id == invoice.Id);
        savedInvoice.Should().NotBeNull();
        savedInvoice!.Status.Should().Be(InvoiceStatus.Draft);
        savedInvoice.RentAmount.Should().Be(_contract.MonthlyRent);
    }

    [Fact]
    public async Task CalculateInvoiceTotal_WithServiceCharges_CalculatesCorrectly()
    {
        // Arrange
        await SetupTestData();
        var service = TestDataBuilder.CreateService(_building.Id, isMetered: true);
        var meter = TestDataBuilder.CreateMeterReading(_room.Id, service.Id, _owner.Id, 3, 2026, 100, 110);

        await _fixture.DbContext.Services.AddAsync(service);
        await _fixture.DbContext.Set<MeterReading>().AddAsync(meter);
        await _fixture.DbContext.SaveChangesAsync();

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            ContractId = _contract.Id,
            BillingMonth = 3,
            BillingYear = 2026,
            RentAmount = _contract.MonthlyRent,
            ServiceAmount = (decimal)(meter.Consumption * service.UnitPrice),
            Status = InvoiceStatus.Draft,
            CreatedBy = _owner.Id
        };

        // Act
        await _fixture.DbContext.Invoices.AddAsync(invoice);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var total = invoice.RentAmount + invoice.ServiceAmount;
        total.Should().Be(_contract.MonthlyRent + (decimal)(meter.Consumption * service.UnitPrice));
    }

    [Fact]
    public async Task UpdateInvoiceStatus_ToPartialPaid_UpdatesSuccessfully()
    {
        // Arrange
        await SetupTestData();
        var invoice = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id);
        await _fixture.DbContext.Invoices.AddAsync(invoice);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        invoice.Status = InvoiceStatus.PartiallyPaid;
        _fixture.DbContext.Invoices.Update(invoice);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var updated = _fixture.DbContext.Invoices.FirstOrDefault(i => i.Id == invoice.Id);
        updated!.Status.Should().Be(InvoiceStatus.PartiallyPaid);
    }

    [Fact]
    public async Task VoidInvoice_WithValidInvoice_MarksAsCancelled()
    {
        // Arrange
        await SetupTestData();
        var invoice = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id);
        await _fixture.DbContext.Invoices.AddAsync(invoice);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        invoice.Status = InvoiceStatus.Void;
        _fixture.DbContext.Invoices.Update(invoice);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var voided = _fixture.DbContext.Invoices.FirstOrDefault(i => i.Id == invoice.Id);
        voided!.Status.Should().Be(InvoiceStatus.Void);
    }

    [Fact]
    public async Task GetInvoices_FiltersByContract_ReturnsOnlyContractInvoices()
    {
        // Arrange
        await SetupTestData();
        var invoice1 = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id, billingMonth: 3); // March
        var invoice2 = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id, billingMonth: 4); // April

        await _fixture.DbContext.Invoices.AddAsync(invoice1);
        await _fixture.DbContext.Invoices.AddAsync(invoice2);
        await _fixture.DbContext.SaveChangesAsync();
    
        // Act
        var invoices = _fixture.DbContext.Invoices
            .Where(i => i.ContractId == _contract.Id)
            .ToList();

        // Assert
        invoices.Should().HaveCount(2);
        invoices.Should().AllSatisfy(i => i.ContractId.Should().Be(_contract.Id));
    }
}
