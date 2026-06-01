using Application.Features.Payments.Commands;
using Domain.Entities;
using Domain.Enums;
using ElysStay.Tests.Integration.TestDoubles;
using FluentAssertions;
using Tests.Integration.Builders;
using Tests.Integration.Fixtures;
using Xunit;

namespace ElysStay.Tests.Integration.Features;

public class PaymentWebhookIntegrationTests : IAsyncLifetime
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
        _invoice = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id, status: InvoiceStatus.Sent);

        await _fixture.DbContext.Users.AddAsync(_owner);
        await _fixture.DbContext.Users.AddAsync(_tenant);
        await _fixture.DbContext.Buildings.AddAsync(building);
        await _fixture.DbContext.Rooms.AddAsync(room);
        await _fixture.DbContext.Contracts.AddAsync(_contract);
        await _fixture.DbContext.Invoices.AddAsync(_invoice);
        await _fixture.DbContext.SaveChangesAsync();
    }

    private ProcessPaymentWebhookCommandHandler CreateHandler()
        => new(
            _fixture.DbContext,
            new FakeCurrentUserService
            {
                UserId = _owner.Id,
                Role = UserRole.Owner,
                Email = _owner.Email,
                FullName = _owner.FullName
            },
            new AllowAllBuildingScopeService(),
            new NoOpEmailService());

    [Fact]
    public async Task ProcessWebhook_WithNewReference_CreatesPaymentOnce_AndMarksInvoicePaid()
    {
        await SetupTestData();
        var handler = CreateHandler();

        var result = await handler.Handle(new ProcessPaymentWebhookCommand
        {
            InvoiceId = _invoice.Id,
            Amount = _invoice.TotalAmount,
            ReferenceCode = "BANK-REF-001",
            Note = "Matched bank webhook"
        }, CancellationToken.None);

        result.PaymentMethod.Should().Be("BankTransfer");
        result.ReferenceCode.Should().Be("BANK-REF-001");

        var payments = _fixture.DbContext.Payments.Where(p => p.InvoiceId == _invoice.Id).ToList();
        payments.Should().HaveCount(1);
        payments[0].ReferenceCode.Should().Be("BANK-REF-001");

        var updatedInvoice = _fixture.DbContext.Invoices.First(i => i.Id == _invoice.Id);
        updatedInvoice.Status.Should().Be(InvoiceStatus.Paid);
    }

    [Fact]
    public async Task ProcessWebhook_WithDuplicateReference_ReturnsExistingPayment_WithoutSecondLedgerEffect()
    {
        await SetupTestData();
        var handler = CreateHandler();

        var command = new ProcessPaymentWebhookCommand
        {
            InvoiceId = _invoice.Id,
            Amount = _invoice.TotalAmount,
            ReferenceCode = "BANK-REF-002",
            Note = "Matched bank webhook"
        };

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        second.Id.Should().Be(first.Id);

        var payments = _fixture.DbContext.Payments.Where(p => p.InvoiceId == _invoice.Id).ToList();
        payments.Should().HaveCount(1);
        payments[0].ReferenceCode.Should().Be("BANK-REF-002");

        var updatedInvoice = _fixture.DbContext.Invoices.First(i => i.Id == _invoice.Id);
        updatedInvoice.Status.Should().Be(InvoiceStatus.Paid);
    }
}