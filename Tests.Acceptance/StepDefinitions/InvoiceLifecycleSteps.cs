using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Invoices.Commands;
using Application.Features.Payments.Commands;
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
/// Step definitions for InvoiceLifecycle.feature.
/// Drives the real SendInvoiceCommandHandler, RecordPaymentCommandHandler and
/// VoidInvoiceCommandHandler against a live PostgreSQL container (DatabaseFixture).
/// ICurrentUserService and IEmailService are mocked; BuildingScopeService is real.
/// Scoped to "Invoice Lifecycle" so its bindings don't collide with other features.
/// </summary>
[Binding]
[Scope(Feature = "Invoice Lifecycle")]
public class InvoiceLifecycleSteps
{
    private readonly DatabaseFixture _fixture;

    private User _owner = null!;
    private User _staff = null!;
    private User _tenant = null!;
    private Building _building = null!;
    private Room _room = null!;
    private Contract _contract = null!;
    private Invoice _invoice = null!;

    private Exception? _lastException;

    public InvoiceLifecycleSteps(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    // ── Identity / handler factories ───────────────────────────────────────────

    private ICurrentUserService Identity(Guid userId, UserRole role)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(m => m.GetRequiredUserId()).Returns(userId);
        mock.Setup(m => m.UserId).Returns(userId);
        mock.Setup(m => m.Role).Returns(role);
        mock.Setup(m => m.IsOwner).Returns(role == UserRole.Owner);
        mock.Setup(m => m.IsStaff).Returns(role == UserRole.Staff);
        mock.Setup(m => m.IsTenant).Returns(role == UserRole.Tenant);
        return mock.Object;
    }

    private static IEmailService NoopEmail()
    {
        var email = new Mock<IEmailService>();
        email.Setup(m => m.TrySendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return email.Object;
    }

    private SendInvoiceCommandHandler SendHandler(ICurrentUserService user)
        => new(_fixture.DbContext, user, new BuildingScopeService(_fixture.DbContext, user), NoopEmail());

    private RecordPaymentCommandHandler PaymentHandler(ICurrentUserService user)
        => new(_fixture.DbContext, user, new BuildingScopeService(_fixture.DbContext, user), NoopEmail());

    private VoidInvoiceCommandHandler VoidHandler(ICurrentUserService user)
        => new(_fixture.DbContext, user, new BuildingScopeService(_fixture.DbContext, user), NoopEmail());

    // ── Background ─────────────────────────────────────────────────────────────

    [Given("the owner has a building with room \"([^\"]*)\" renting at ([0-9]+) VND and an active tenant contract")]
    public async Task GivenOwnerBuildingRoomAndContract(string roomNumber, decimal rent)
    {
        _owner = TestDataBuilder.CreateUser(role: UserRole.Owner);
        _staff = TestDataBuilder.CreateUser(role: UserRole.Staff);
        _tenant = TestDataBuilder.CreateUser(role: UserRole.Tenant);
        _building = TestDataBuilder.CreateBuilding(_owner.Id);
        _room = TestDataBuilder.CreateRoom(_building.Id, roomNumber: roomNumber, price: rent);
        _room.Status = RoomStatus.Occupied;
        _contract = TestDataBuilder.CreateContract(
            _room.Id, _tenant.Id, _owner.Id,
            monthlyRent: rent,
            StartDate: new DateOnly(2026, 1, 1),
            MoveInDate: new DateOnly(2026, 1, 1),
            EndDate: new DateOnly(2027, 12, 31));

        _fixture.DbContext.Users.AddRange(_owner, _staff, _tenant);
        _fixture.DbContext.Buildings.Add(_building);
        _fixture.DbContext.Rooms.Add(_room);
        _fixture.DbContext.Contracts.Add(_contract);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    [Given("an invoice in \"([^\"]*)\" status")]
    public async Task GivenAnInvoiceInStatus(string status)
    {
        var parsed = Enum.Parse<InvoiceStatus>(status, ignoreCase: true);
        _invoice = TestDataBuilder.CreateInvoice(
            _contract.Id, _owner.Id, billingMonth: 3, billingYear: 2026,
            rentAmount: _room.Price, status: parsed);
        _fixture.DbContext.Invoices.Add(_invoice);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    // ── When ───────────────────────────────────────────────────────────────────

    [When("the owner sends the invoice")]
    public async Task WhenOwnerSendsInvoice()
        => await Capture(() => SendHandler(Identity(_owner.Id, UserRole.Owner))
            .Handle(new SendInvoiceCommand(_invoice.Id), default));

    [When("the owner records a payment of ([0-9]+) VND")]
    public async Task WhenOwnerRecordsPayment(decimal amount)
        => await Capture(() => PaymentHandler(Identity(_owner.Id, UserRole.Owner))
            .Handle(new RecordPaymentCommand { InvoiceId = _invoice.Id, Amount = amount }, default));

    [When("the owner voids the invoice")]
    public async Task WhenOwnerVoidsInvoice()
        => await Capture(() => VoidHandler(Identity(_owner.Id, UserRole.Owner))
            .Handle(new VoidInvoiceCommand(_invoice.Id), default));

    [When("a staff member voids the invoice")]
    public async Task WhenStaffVoidsInvoice()
        => await Capture(() => VoidHandler(Identity(_staff.Id, UserRole.Staff))
            .Handle(new VoidInvoiceCommand(_invoice.Id), default));

    [When("the tenant voids the invoice")]
    public async Task WhenTenantVoidsInvoice()
        => await Capture(() => VoidHandler(Identity(_tenant.Id, UserRole.Tenant))
            .Handle(new VoidInvoiceCommand(_invoice.Id), default));

    private async Task Capture(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _lastException = ex;
        }
    }

    // ── Then ───────────────────────────────────────────────────────────────────

    [Then("the invoice status should be \"([^\"]*)\"")]
    public async Task ThenInvoiceStatusShouldBe(string status)
    {
        var dbInvoice = await _fixture.DbContext.Invoices.AsNoTracking()
            .FirstAsync(i => i.Id == _invoice.Id);
        dbInvoice.Status.ToString().Should().Be(status);
    }

    [Then("the operation should be rejected")]
    public void ThenOperationShouldBeRejected()
        => _lastException.Should().NotBeNull("the operation should have thrown");

    [Then("the operation should be rejected with a permission error")]
    public void ThenOperationShouldBeRejectedWithPermissionError()
        => _lastException.Should().BeOfType<ForbiddenException>();

    [Then("a payment of ([0-9]+) VND should be recorded")]
    public async Task ThenPaymentShouldBeRecorded(decimal amount)
    {
        var total = await _fixture.DbContext.Payments
            .Where(p => p.InvoiceId == _invoice.Id && p.Type == PaymentType.RentPayment)
            .SumAsync(p => p.Amount);
        total.Should().Be(amount);
    }

    [Then("no payment should be recorded")]
    public async Task ThenNoPaymentShouldBeRecorded()
    {
        var count = await _fixture.DbContext.Payments
            .CountAsync(p => p.InvoiceId == _invoice.Id);
        count.Should().Be(0);
    }
}
