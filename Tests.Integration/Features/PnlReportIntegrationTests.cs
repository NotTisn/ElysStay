using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Dashboard.Queries;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Tests.Integration.Builders;
using Tests.Integration.Fixtures;
using Xunit;

namespace ElysStay.Tests.Integration.Features;

/// <summary>
/// Integration tests for the financial reporting process (1.5.7 – 1.5.8), driven through
/// the real GetPnlReportQueryHandler against a real PostgreSQL database.
///
/// Verifies the financial metrics defined in 1.5.8:
///   • Operating Revenue  = RENT_PAYMENT received
///   • Deposit In/Refund  = DEPOSIT_IN / DEPOSIT_REFUND
///   • Expenses           = Expense table
///   • PNL-02 Net Operating Profit = Operating Revenue − Expenses (deposits excluded)
///   • PNL-03 Net Cash Flow        = Operating Revenue + Deposit In − Deposit Refund − Expenses
///   • PNL-04 payments tied to a VOID invoice are excluded
///   • Report is Owner-only and scoped to buildings the owner owns.
/// </summary>
public class PnlReportIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();

    private User _owner = null!;
    private User _tenant = null!;
    private Building _building = null!;
    private Room _room = null!;
    private Contract _contract = null!;

    private const int Year = 2026;
    private const int Month = 3;
    private static DateTime MarchDay(int day) => new(Year, Month, day, 10, 0, 0, DateTimeKind.Utc);

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    private async Task SetupTestData()
    {
        _owner = TestDataBuilder.CreateUser(role: UserRole.Owner);
        _tenant = TestDataBuilder.CreateUser(role: UserRole.Tenant);
        _building = TestDataBuilder.CreateBuilding(_owner.Id);
        _room = TestDataBuilder.CreateRoom(_building.Id);
        _room.Status = RoomStatus.Occupied;
        _contract = TestDataBuilder.CreateContract(_room.Id, _tenant.Id, _owner.Id);

        _fixture.DbContext.Users.AddRange(_owner, _tenant);
        _fixture.DbContext.Buildings.Add(_building);
        _fixture.DbContext.Rooms.Add(_room);
        _fixture.DbContext.Contracts.Add(_contract);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    private GetPnlReportQueryHandler CreateHandler(Guid? ownerId = null, UserRole role = UserRole.Owner)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(m => m.GetRequiredUserId()).Returns(ownerId ?? _owner.Id);
        currentUser.Setup(m => m.UserId).Returns(ownerId ?? _owner.Id);
        currentUser.Setup(m => m.Role).Returns(role);
        currentUser.Setup(m => m.IsOwner).Returns(role == UserRole.Owner);
        return new GetPnlReportQueryHandler(_fixture.DbContext, currentUser.Object);
    }

    /// <summary>
    /// Seeds, for March 2026:
    ///   • a non-void invoice with a 10,000,000 rent payment (counts as Operating Revenue)
    ///   • a VOID invoice with a 4,000,000 rent payment (must be excluded — PNL-04)
    ///   • a 5,000,000 DEPOSIT_IN and a 2,000,000 DEPOSIT_REFUND on the contract
    ///   • a 3,000,000 expense
    /// </summary>
    private async Task SeedMarchActivity()
    {
        var validInvoice = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id,
            billingMonth: Month, billingYear: Year, rentAmount: 10_000_000, status: InvoiceStatus.Sent);
        var voidInvoice = TestDataBuilder.CreateInvoice(_contract.Id, _owner.Id,
            billingMonth: Month, billingYear: Year, rentAmount: 4_000_000, status: InvoiceStatus.Void);
        _fixture.DbContext.Invoices.AddRange(validInvoice, voidInvoice);

        _fixture.DbContext.Payments.AddRange(
            new Payment { InvoiceId = validInvoice.Id, Type = PaymentType.RentPayment, Amount = 10_000_000, PaidAt = MarchDay(5), RecordedBy = _owner.Id },
            new Payment { InvoiceId = voidInvoice.Id, Type = PaymentType.RentPayment, Amount = 4_000_000, PaidAt = MarchDay(6), RecordedBy = _owner.Id },
            new Payment { ContractId = _contract.Id, Type = PaymentType.DepositIn, Amount = 5_000_000, PaidAt = MarchDay(1), RecordedBy = _owner.Id },
            new Payment { ContractId = _contract.Id, Type = PaymentType.DepositRefund, Amount = 2_000_000, PaidAt = MarchDay(20), RecordedBy = _owner.Id });

        _fixture.DbContext.Expenses.Add(new Expense
        {
            BuildingId = _building.Id,
            Category = "Maintenance",
            Description = "Bảo trì tháng 3",
            Amount = 3_000_000,
            ExpenseDate = new DateOnly(Year, Month, 15),
            RecordedBy = _owner.Id
        });

        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    // ── PNL-02 / PNL-03 / PNL-04 aggregation ──────────────────────────────────

    [Fact]
    public async Task PnlReport_March_AggregatesRevenueDepositsAndExpenses_ExcludingVoidInvoice()
    {
        await SetupTestData();
        await SeedMarchActivity();

        var report = await CreateHandler().Handle(new GetPnlReportQuery { Year = Year }, default);

        var march = report.Months.Single(m => m.Month == Month);

        // Operating Revenue excludes the void-invoice payment (PNL-04)
        march.OperationalIncome.Should().Be(10_000_000);
        march.DepositsReceived.Should().Be(5_000_000);
        march.DepositsRefunded.Should().Be(2_000_000);
        march.Expenses.Should().Be(3_000_000);

        // PNL-02: deposits are excluded from operating profit
        march.NetOperational.Should().Be(7_000_000);   // 10,000,000 − 3,000,000

        // PNL-03: cash flow includes deposits
        march.NetCashFlow.Should().Be(10_000_000);      // 10M + 5M − 2M − 3M
    }

    [Fact]
    public async Task PnlReport_ReturnsTwelveMonths_WithZeroForInactiveMonths()
    {
        await SetupTestData();
        await SeedMarchActivity();

        var report = await CreateHandler().Handle(new GetPnlReportQuery { Year = Year }, default);

        report.Months.Should().HaveCount(12);

        var january = report.Months.Single(m => m.Month == 1);
        january.OperationalIncome.Should().Be(0);
        january.NetOperational.Should().Be(0);
        january.NetCashFlow.Should().Be(0);
    }

    [Fact]
    public async Task PnlReport_DepositOnlyMonth_HasZeroOperatingProfit_ButPositiveCashFlow()
    {
        await SetupTestData();
        // April: only a deposit received, nothing else
        _fixture.DbContext.Payments.Add(new Payment
        {
            ContractId = _contract.Id,
            Type = PaymentType.DepositIn,
            Amount = 5_000_000,
            PaidAt = new DateTime(Year, 4, 2, 10, 0, 0, DateTimeKind.Utc),
            RecordedBy = _owner.Id
        });
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        var report = await CreateHandler().Handle(new GetPnlReportQuery { Year = Year }, default);
        var april = report.Months.Single(m => m.Month == 4);

        april.OperationalIncome.Should().Be(0);
        april.NetOperational.Should().Be(0);
        april.NetCashFlow.Should().Be(5_000_000);
    }

    // ── Authorization (Owner-only, building-scoped) ───────────────────────────

    [Fact]
    public async Task PnlReport_NonOwner_ThrowsForbidden()
    {
        await SetupTestData();

        var act = () => CreateHandler(ownerId: _tenant.Id, role: UserRole.Tenant)
            .Handle(new GetPnlReportQuery { Year = Year }, default);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task PnlReport_BuildingNotOwnedByUser_ThrowsForbidden()
    {
        await SetupTestData();

        var act = () => CreateHandler()
            .Handle(new GetPnlReportQuery { Year = Year, BuildingId = Guid.NewGuid() }, default);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
