using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Contracts.Commands;
using Application.Features.Contracts.DTOs;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Moq;
using TechTalk.SpecFlow;
using Tests.Integration.Builders;
using Tests.Integration.Fixtures;
using Xunit;

namespace ElysStay.Tests.Acceptance.StepDefinitions;

/// <summary>
/// Step definitions for MoveOutProcess.feature.
/// Uses real TerminateContractCommandHandler against a live PostgreSQL container.
/// ICurrentUserService and IEmailService are mocked.
/// </summary>
[Binding]
[Scope(Feature = "Move-out Process")]
public class MoveOutProcessSteps
{
    private readonly DatabaseFixture _fixture;

    private User     _owner    = null!;
    private Building _building = null!;
    private Room     _room     = null!;
    private User     _tenant   = null!;
    private Contract _contract = null!;

    private decimal     _deductions;
    private string?     _terminationNote;
    private ContractDto? _result;
    private Exception?  _lastException;

    public MoveOutProcessSteps(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    // ── Handler factory ────────────────────────────────────────────────────────

    private TerminateContractCommandHandler CreateHandler()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(m => m.GetRequiredUserId()).Returns(_owner.Id);
        currentUser.Setup(m => m.IsOwner).Returns(true);

        var emailService = new Mock<IEmailService>();
        emailService
            .Setup(m => m.TrySendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        return new TerminateContractCommandHandler(
            _fixture.DbContext, currentUser.Object,
            new BuildingScopeService(_fixture.DbContext, currentUser.Object),
            emailService.Object);
    }

    // ── Background steps ───────────────────────────────────────────────────────

    [Given("a building owner for move-out tests")]
    public async Task GivenABuildingOwnerForMoveOutTests()
    {
        _owner = TestDataBuilder.CreateUser(role: UserRole.Owner);
        await _fixture.DbContext.Users.AddAsync(_owner);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given("a building for move-out tests")]
    public async Task GivenABuildingForMoveOutTests()
    {
        _building = TestDataBuilder.CreateBuilding(_owner.Id);
        await _fixture.DbContext.Buildings.AddAsync(_building);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given("a room \"([^\"]*)\" with deposit ([0-9]+) VND")]
    public async Task GivenARoomWithDeposit(string roomNumber, decimal deposit)
    {
        _room = TestDataBuilder.CreateRoom(_building.Id, roomNumber: roomNumber);
        _room.Status = RoomStatus.Occupied;
        await _fixture.DbContext.Rooms.AddAsync(_room);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given("a tenant for move-out tests")]
    public async Task GivenATenantForMoveOutTests()
    {
        _tenant = TestDataBuilder.CreateUser(role: UserRole.Tenant);
        await _fixture.DbContext.Users.AddAsync(_tenant);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given("an active contract with deposit ([0-9]+) VND")]
    public async Task GivenAnActiveContractWithDeposit(decimal deposit)
    {
        _contract = TestDataBuilder.CreateContract(
            _room.Id, _tenant.Id, _owner.Id,
            depositAmount: deposit,
            StartDate:  new DateOnly(2026, 1, 1),
            MoveInDate: new DateOnly(2026, 1, 1),
            EndDate:    new DateOnly(2027, 12, 31));
        await _fixture.DbContext.Contracts.AddAsync(_contract);
        await _fixture.DbContext.SaveChangesAsync();
    }

    // ── Deduction / note Given steps ───────────────────────────────────────────

    [Given("no deductions on move-out")]
    public void GivenNoDeductions() => _deductions = 0;

    [Given("deductions of ([0-9]+) VND for damages")]
    public void GivenDeductionsForDamages(decimal amount) => _deductions = amount;

    [Given("deductions of (-[0-9]+) VND for damages")]
    public void GivenNegativeDeductionsForDamages(decimal amount) => _deductions = amount;

    [Given("a note \"([^\"]*)\"")]
    public void GivenANote(string note) => _terminationNote = note;

    // ── Room status setup steps ────────────────────────────────────────────────

    [Given("the room is occupied by the tenant")]
    public async Task GivenTheRoomIsOccupied()
    {
        _room.Status = RoomStatus.Occupied;
        _fixture.DbContext.Rooms.Update(_room);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given("the room is in maintenance status")]
    public async Task GivenTheRoomIsInMaintenance()
    {
        _room.Status = RoomStatus.Maintenance;
        _fixture.DbContext.Rooms.Update(_room);
        await _fixture.DbContext.SaveChangesAsync();
    }

    // ── Contract tenant setup ──────────────────────────────────────────────────

    [Given("the contract has ([0-9]+) active occupants")]
    public async Task GivenTheContractHasActiveOccupants(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var occupant = TestDataBuilder.CreateUser(role: UserRole.Tenant);
            await _fixture.DbContext.Users.AddAsync(occupant);
            await _fixture.DbContext.SaveChangesAsync();

            _fixture.DbContext.ContractTenants.Add(new ContractTenant
            {
                Id           = Guid.NewGuid(),
                ContractId   = _contract.Id,
                TenantUserId = occupant.Id,
                IsMainTenant = i == 0,
                MoveInDate   = new DateOnly(2026, 1, 1)
            });
        }
        await _fixture.DbContext.SaveChangesAsync();
    }

    // ── Invoice setup steps ────────────────────────────────────────────────────

    [Given("there is a Draft invoice for July 2026")]
    public async Task GivenDraftInvoiceForJuly()
        => await AddInvoice(2026, 7, InvoiceStatus.Draft);

    [Given("there is a Sent invoice for August 2026")]
    public async Task GivenSentInvoiceForAugust()
        => await AddInvoice(2026, 8, InvoiceStatus.Sent);

    [Given("there is a Draft invoice for June 2026")]
    public async Task GivenDraftInvoiceForJune()
        => await AddInvoice(2026, 6, InvoiceStatus.Draft);

    [Given("there is a Sent invoice for July 2026 with a payment")]
    public async Task GivenSentInvoiceForJulyWithPayment()
    {
        var invoice = await AddInvoice(2026, 7, InvoiceStatus.Sent);
        var payment = TestDataBuilder.CreatePayment(invoice.Id, _owner.Id, 1_000_000);
        _fixture.DbContext.Payments.Add(payment);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given("the contract is already terminated")]
    public async Task GivenTheContractIsAlreadyTerminated()
    {
        _contract.Status = ContractStatus.Terminated;
        _contract.TerminationDate = new DateOnly(2026, 3, 31);
        _fixture.DbContext.Contracts.Update(_contract);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    // ── When steps ─────────────────────────────────────────────────────────────

    [When("I terminate the contract on \"([0-9-]+)\"")]
    public async Task WhenITerminateTheContract(string dateStr)
    {
        try
        {
            _result = await CreateHandler().Handle(new TerminateContractCommand
            {
                Id = _contract.Id,
                TerminationDate = DateOnly.Parse(dateStr),
                Deductions = _deductions,
                Note = _terminationNote
            }, default);
        }
        catch (Exception ex)
        {
            _lastException = ex;
        }
    }

    [When("I try to terminate the contract on \"([0-9-]+)\"")]
    public async Task WhenITryToTerminateTheContract(string dateStr)
        => await WhenITerminateTheContract(dateStr);

    // ── Then — deposit assertions ──────────────────────────────────────────────

    [Then("the deposit status should be \"([^\"]*)\"")]
    public async Task ThenDepositStatusShouldBe(string status)
    {
        _lastException.Should().BeNull();
        _result.Should().NotBeNull();
        _result!.DepositStatus.Should().Be(status);

        var dbContract = await _fixture.DbContext.Contracts.FindAsync(_contract.Id);
        dbContract!.DepositStatus.ToString().Should().Be(status);
    }

    [Then("the refund amount should be ([0-9]+) VND")]
    public async Task ThenRefundAmountShouldBe(decimal expected)
    {
        _lastException.Should().BeNull();
        _result.Should().NotBeNull();
        _result!.RefundAmount.Should().Be(expected);

        var dbContract = await _fixture.DbContext.Contracts.FindAsync(_contract.Id);
        dbContract!.RefundAmount.Should().Be(expected);
    }

    [Then("a DEPOSIT_REFUND payment of ([0-9]+) VND should be created")]
    public async Task ThenDepositRefundPaymentShouldBeCreated(decimal expectedAmount)
    {
        var payment = await _fixture.DbContext.Payments
            .FirstOrDefaultAsync(p => p.ContractId == _contract.Id && p.Type == PaymentType.DepositRefund);
        payment.Should().NotBeNull("a DEPOSIT_REFUND payment must exist in the database");
        payment!.Amount.Should().Be(expectedAmount);
    }

    [Then("an audit DEPOSIT_REFUND payment of ([0-9]+) VND should be created")]
    public async Task ThenAuditDepositRefundPaymentShouldBeCreated(decimal expectedAmount)
        => await ThenDepositRefundPaymentShouldBeCreated(expectedAmount);

    // ── Then — contract/room state assertions ──────────────────────────────────

    [Then("the contract status should be \"([^\"]*)\"")]
    public async Task ThenContractStatusShouldBe(string status)
    {
        _lastException.Should().BeNull();
        _result.Should().NotBeNull();
        _result!.Status.Should().Be(status);

        var dbContract = await _fixture.DbContext.Contracts.FindAsync(_contract.Id);
        dbContract!.Status.ToString().Should().Be(status);
    }

    [Then("the termination date should be \"([0-9-]+)\"")]
    public async Task ThenTerminationDateShouldBe(string dateStr)
    {
        var expected = DateOnly.Parse(dateStr);
        _result!.TerminationDate.Should().Be(expected);

        var dbContract = await _fixture.DbContext.Contracts.FindAsync(_contract.Id);
        dbContract!.TerminationDate.Should().Be(expected);
    }

    [Then("the room status should be \"([^\"]*)\"")]
    public async Task ThenRoomStatusShouldBe(string status)
    {
        var expected = Enum.Parse<RoomStatus>(status);
        var dbRoom = await _fixture.DbContext.Rooms.FindAsync(_room.Id);
        dbRoom!.Status.Should().Be(expected);
    }

    // ── Then — contract tenant assertions ─────────────────────────────────────

    [Then("all active occupants should have move-out date \"([0-9-]+)\"")]
    public async Task ThenAllOccupantsShouldHaveMoveOutDate(string dateStr)
    {
        var expected = DateOnly.Parse(dateStr);
        var tenants = await _fixture.DbContext.ContractTenants
            .Where(ct => ct.ContractId == _contract.Id)
            .ToListAsync();
        tenants.Should().NotBeEmpty();
        tenants.Should().AllSatisfy(ct => ct.MoveOutDate.Should().Be(expected));
    }

    // ── Then — invoice status assertions ──────────────────────────────────────

    [Then("the July 2026 invoice should be Void")]
    public async Task ThenJulyInvoiceShouldBeVoid()
        => await AssertInvoiceStatus(2026, 7, InvoiceStatus.Void);

    [Then("the August 2026 invoice should be Void")]
    public async Task ThenAugustInvoiceShouldBeVoid()
        => await AssertInvoiceStatus(2026, 8, InvoiceStatus.Void);

    [Then("the June 2026 invoice should still be Draft")]
    public async Task ThenJuneInvoiceShouldBeDraft()
        => await AssertInvoiceStatus(2026, 6, InvoiceStatus.Draft);

    [Then("the July 2026 invoice should still be Sent")]
    public async Task ThenJulyInvoiceShouldBeSent()
        => await AssertInvoiceStatus(2026, 7, InvoiceStatus.Sent);

    // ── Then — validation error assertions ────────────────────────────────────

    [Then("the move-out should be rejected with conflict error")]
    public void ThenMoveOutRejectedWithConflict()
    {
        _lastException.Should().NotBeNull();
        _lastException.Should().BeOfType<ConflictException>();
    }

    [Then("the move-out should be rejected with validation error")]
    public void ThenMoveOutRejectedWithValidation()
    {
        _lastException.Should().NotBeNull();
        (_lastException is BadRequestException or ConflictException).Should().BeTrue(
            "a validation or conflict exception must be thrown");
    }

    // ── Then — note assertion ──────────────────────────────────────────────────

    [Then("the termination note \"([^\"]*)\" should be saved")]
    public async Task ThenTerminationNoteShouldBeSaved(string note)
    {
        _lastException.Should().BeNull();

        var dbContract = await _fixture.DbContext.Contracts.FindAsync(_contract.Id);
        dbContract!.TerminationNote.Should().Be(note);

        var refundPayment = await _fixture.DbContext.Payments
            .FirstAsync(p => p.ContractId == _contract.Id && p.Type == PaymentType.DepositRefund);
        refundPayment.Note.Should().Be(note);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<Invoice> AddInvoice(int year, int month, InvoiceStatus status)
    {
        var invoice = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id, month, year, status: status);
        _fixture.DbContext.Invoices.Add(invoice);
        await _fixture.DbContext.SaveChangesAsync();
        return invoice;
    }

    private async Task AssertInvoiceStatus(int year, int month, InvoiceStatus expected)
    {
        _lastException.Should().BeNull();
        var invoice = await _fixture.DbContext.Invoices
            .FirstAsync(i => i.ContractId == _contract.Id && i.BillingYear == year && i.BillingMonth == month);
        invoice.Status.Should().Be(expected);
    }
}
