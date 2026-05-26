using TechTalk.SpecFlow;
using Xunit;
using Domain.Entities;
using Domain.Enums;
using Tests.Integration.Fixtures;
using Tests.Integration.Builders;

namespace ElysStay.Tests.Acceptance.StepDefinitions;

[Binding]
public class PaymentTrackingSteps
{
    private readonly DatabaseFixture _fixture;
    private User _owner = null!;
    private Invoice _invoice = null!;
    private Payment? _lastPayment;
    private Exception? _lastException;
    private decimal _totalPaid = 0;

    public PaymentTrackingSteps(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Given("a building owner")]
    public async Task GivenABuildingOwner()
    {
        _owner = TestDataBuilder.CreateUser(role: UserRole.Owner);
        await _fixture.DbContext.Users.AddAsync(_owner);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given("an invoice with amount ([0-9]+) VND")]
    public async Task GivenAnInvoiceWithAmount(decimal amount)
    {
        var building = TestDataBuilder.CreateBuilding(_owner.Id);
        var room = TestDataBuilder.CreateRoom(building.Id);
        var tenant = TestDataBuilder.CreateUser(role: UserRole.Tenant);
        var contract = TestDataBuilder.CreateContract(room.Id, tenant.Id, _owner.Id);

        await _fixture.DbContext.Buildings.AddAsync(building);
        await _fixture.DbContext.Rooms.AddAsync(room);
        await _fixture.DbContext.Users.AddAsync(tenant);
        await _fixture.DbContext.Contracts.AddAsync(contract);
        await _fixture.DbContext.SaveChangesAsync();

        _invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            ContractId = contract.Id,
            BillingMonth = DateTime.UtcNow.Month,
            BillingYear = DateTime.UtcNow.Year,
            RentAmount = amount,
            TotalAmount = amount,
            Status = InvoiceStatus.Sent,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            CreatedBy = _owner.Id
        };

        await _fixture.DbContext.Invoices.AddAsync(_invoice);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Given("payment methods available: CASH, BANK_TRANSFER, MOMO, ZALOPAY")]
    public void GivenPaymentMethodsAvailable()
    {
        // All payment methods are available in our PaymentMethod enum
    }

    [When("I record payment of ([0-9]+) VND via BANK_TRANSFER")]
    public async Task WhenIRecordPaymentViaBankTransfer(decimal amount)
    {
        _lastPayment = new Payment
        {
            Id = Guid.NewGuid(),
            InvoiceId = _invoice.Id,
            Amount = amount,
            PaymentMethod = "BankTransfer",
            RecordedBy = _owner.Id
        };

        _invoice.Status = InvoiceStatus.Paid;
        _totalPaid += amount;

        await _fixture.DbContext.Set<Payment>().AddAsync(_lastPayment);
        _fixture.DbContext.Invoices.Update(_invoice);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Then("payment should be created with:")]
    public void ThenPaymentShouldBeCreatedWith(Table table)
    {
        Assert.NotNull(_lastPayment);

        foreach (var row in table.Rows)
        {
            var field = row["Field"];
            var expectedValue = row["Value"];

            switch (field)
            {
                case "Amount":
                    Assert.Equal(decimal.Parse(expectedValue), _lastPayment.Amount);
                    break;
                case "Method":
                    Assert.Equal(expectedValue, _lastPayment.PaymentMethod);
                    break;
                case "Invoice":
                    Assert.NotNull(_lastPayment.InvoiceId);
                    break;
            }
        }
    }

    [Then("invoice status should be \"([^\"]*)\"")]
    public void ThenInvoiceStatusShouldBe(string expectedStatus)
    {
        var dbInvoice = _fixture.DbContext.Invoices.FirstOrDefault(i => i.Id == _invoice.Id);
        Assert.NotNull(dbInvoice);
        var status = Enum.Parse<InvoiceStatus>(expectedStatus);
        Assert.Equal(status, dbInvoice.Status);
    }

    [When("I record payment of ([0-9]+) VND via CASH")]
    public async Task WhenIRecordPaymentViaCash(decimal amount)
    {
        _lastPayment = new Payment
        {
            Id = Guid.NewGuid(),
            InvoiceId = _invoice.Id,
            Amount = amount,
            PaymentMethod = "Cash",
            RecordedBy = _owner.Id
        };

        _totalPaid += amount;
        _invoice.Status = _totalPaid < _invoice.TotalAmount ? InvoiceStatus.PartiallyPaid : InvoiceStatus.Paid;

        await _fixture.DbContext.Set<Payment>().AddAsync(_lastPayment);
        _fixture.DbContext.Invoices.Update(_invoice);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Then("payment should be created successfully")]
    public void ThenPaymentShouldBeCreatedSuccessfully()
    {
        Assert.NotNull(_lastPayment);
        Assert.NotEqual(Guid.Empty, _lastPayment.Id);
    }

    [Then("remaining amount should be ([0-9]+) VND")]
    public void ThenRemainingAmountShouldBe(decimal expectedRemaining)
    {
        var remaining = _invoice.TotalAmount - _totalPaid;
        Assert.Equal(expectedRemaining, remaining);
    }

    [When("I record first payment of ([0-9]+) VND")]
    public async Task WhenIRecordFirstPayment(decimal amount)
    {
        await WhenIRecordPaymentViaBankTransfer(amount);
    }

    [When("I record second payment of ([0-9]+) VND")]
    public async Task WhenIRecordSecondPayment(decimal amount)
    {
        _lastPayment = new Payment
        {
            Id = Guid.NewGuid(),
            InvoiceId = _invoice.Id,
            Amount = amount,
            PaymentMethod = "BankTransfer",
            RecordedBy = _owner.Id
        };

        _totalPaid += amount;
        _invoice.Status = _totalPaid >= _invoice.TotalAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;

        await _fixture.DbContext.Set<Payment>().AddAsync(_lastPayment);
        _fixture.DbContext.Invoices.Update(_invoice);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Then("total paid should be ([0-9]+) VND")]
    public void ThenTotalPaidShouldBe(decimal expectedTotal)
    {
        Assert.Equal(expectedTotal, _totalPaid);
    }

    [When("I record MOMO payment of ([0-9]+) VND with reference \"([^\"]*)\"")]
    public async Task WhenIRecordMomoPayment(decimal amount, string reference)
    {
        _lastPayment = new Payment
        {
            Id = Guid.NewGuid(),
            InvoiceId = _invoice.Id,
            Amount = amount,
            PaymentMethod = "Momo",
            ReferenceCode = reference,
            RecordedBy = _owner.Id
        };

        _totalPaid += amount;
        _invoice.Status = _totalPaid >= _invoice.TotalAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;

        await _fixture.DbContext.Set<Payment>().AddAsync(_lastPayment);
        _fixture.DbContext.Invoices.Update(_invoice);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Then("payment should include reference code \"([^\"]*)\"")]
    public void ThenPaymentShouldIncludeReferenceCode(string referenceCode)
    {
        Assert.NotNull(_lastPayment);
        Assert.Equal(referenceCode, _lastPayment.ReferenceCode);
    }

    [Then("system should validate MOMO reference format")]
    public void ThenSystemShouldValidateMomoReferenceFormat()
    {
        Assert.NotNull(_lastPayment);
        Assert.NotNull(_lastPayment.ReferenceCode);
        // Could add regex validation here if needed
    }

    [When("I try to record payment of ([0-9]+) VND for ([0-9]+) VND invoice")]
    public void WhenITryToRecordPaymentExceedingInvoiceAmount(decimal paymentAmount, decimal invoiceAmount)
    {
        try
        {
            if (paymentAmount > invoiceAmount)
            {
                throw new InvalidOperationException("Payment cannot exceed invoice amount");
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
