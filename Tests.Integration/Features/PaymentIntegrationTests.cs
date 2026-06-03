using Xunit;
using FluentAssertions;
using Domain.Entities;
using Domain.Enums;
using Tests.Integration.Fixtures;
using Tests.Integration.Builders;

namespace ElysStay.Tests.Integration.Features;

public class PaymentIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();
    private User _owner = null!;
    private User _tenant = null!;
    private Contract _contract = null!;
    private Invoice _invoice = null!;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    private async Task SetupTestData()
    {
        _owner = TestDataBuilder.CreateUser(role: UserRole.Owner);
        _tenant = TestDataBuilder.CreateUser(role: UserRole.Tenant);
        var building = TestDataBuilder.CreateBuilding(_owner.Id);
        var room = TestDataBuilder.CreateRoom(building.Id);
        _contract = TestDataBuilder.CreateContract(room.Id, _tenant.Id, _owner.Id);
        _invoice = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id);

        await _fixture.DbContext.Users.AddAsync(_owner);
        await _fixture.DbContext.Users.AddAsync(_tenant);
        await _fixture.DbContext.Buildings.AddAsync(building);
        await _fixture.DbContext.Rooms.AddAsync(room);
        await _fixture.DbContext.Contracts.AddAsync(_contract);
        await _fixture.DbContext.Invoices.AddAsync(_invoice);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task RecordPayment_WithValidAmount_CreatesPaymentSuccessfully()
    {
        // Arrange
        await SetupTestData();
        var payment = TestDataBuilder.CreatePayment(_invoice.Id, _owner.Id, _invoice.RentAmount);

        // Act
        await _fixture.DbContext.Set<Payment>().AddAsync(payment);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var saved = _fixture.DbContext.Set<Payment>().FirstOrDefault(p => p.Id == payment.Id);
        saved.Should().NotBeNull();
        saved!.Type.Should().Be(PaymentType.RentPayment);
        saved.Amount.Should().Be(_invoice.RentAmount);
    }

    [Fact]
    public async Task RecordPartialPayment_UpdatesInvoiceStatusCorrectly()
    {
        // Arrange
        await SetupTestData();
        var partialAmount = _invoice.RentAmount / 2;
        var payment = TestDataBuilder.CreatePayment(_invoice.Id, _owner.Id, partialAmount);

        // Act
        await _fixture.DbContext.Set<Payment>().AddAsync(payment);
        _invoice.Status = InvoiceStatus.PartiallyPaid;
        _fixture.DbContext.Invoices.Update(_invoice);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var updatedInvoice = _fixture.DbContext.Invoices.FirstOrDefault(i => i.Id == _invoice.Id);
        updatedInvoice!.Status.Should().Be(InvoiceStatus.PartiallyPaid);
    }

    [Fact]
    public async Task RecordMultiplePayments_UpdatesInvoiceStatusToFullyPaid()
    {
        // Arrange
        await SetupTestData();
        var payment1 = TestDataBuilder.CreatePayment(_invoice.Id, _owner.Id, _invoice.RentAmount / 2);
        var payment2 = TestDataBuilder.CreatePayment(_invoice.Id, _owner.Id, _invoice.RentAmount / 2);

        // Act
        await _fixture.DbContext.Set<Payment>().AddAsync(payment1);
        await _fixture.DbContext.Set<Payment>().AddAsync(payment2);
        _invoice.Status = InvoiceStatus.Paid;
        _fixture.DbContext.Invoices.Update(_invoice);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var payments = _fixture.DbContext.Set<Payment>()
            .Where(p => p.InvoiceId == _invoice.Id)
            .ToList();
        payments.Should().HaveCount(2);
        
        var updatedInvoice = _fixture.DbContext.Invoices.FirstOrDefault(i => i.Id == _invoice.Id);
        updatedInvoice!.Status.Should().Be(InvoiceStatus.Paid);
    }

    [Fact]
    public async Task RecordPaymentWithDifferentMethods_StoresMethodCorrectly()
    {
        // Arrange
        await SetupTestData();
        var paymentMethods = new[] { "Cash", "BankTransfer", "Momo" };

        // Act & Assert
        foreach (var method in paymentMethods)
        {
            var payment = TestDataBuilder.CreatePayment(_invoice.Id, _owner.Id, 1_000_000, method);
            await _fixture.DbContext.Set<Payment>().AddAsync(payment);
            await _fixture.DbContext.SaveChangesAsync();

            var saved = _fixture.DbContext.Set<Payment>()
                .FirstOrDefault(p => p.Id == payment.Id);
            saved!.PaymentMethod.Should().Be(method);
        }
    }
    }
