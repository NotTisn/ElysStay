using Application.Common.Interfaces;
using Application.Features.Invoices.Commands;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using MockQueryable.Moq;
using Moq;
using Xunit;

namespace ElysStay.Tests.Unit.Business;

/// <summary>
/// Unit tests for DueDate calculation logic in GenerateInvoicesCommandHandler.
/// DueDate = first day of (billing month + 1) with day = Building.InvoiceDueDay,
/// clamped to the last valid day of that month.
/// </summary>
public class InvoiceDueDateUnitTests
{
    private readonly Mock<IApplicationDbContext> _db = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IBuildingScopeService> _buildingScope = new();

    private GenerateInvoicesCommandHandler CreateHandler()
        => new(_db.Object, _currentUser.Object, _buildingScope.Object);

    private void SetupMocks(int invoiceDueDay, out Guid buildingId)
    {
        var bId = Guid.NewGuid();
        buildingId = bId;

        var building = new Building { Id = bId, Name = "Tòa Test", InvoiceDueDay = invoiceDueDay };
        var room = new Room { Id = Guid.NewGuid(), BuildingId = bId, Building = building, RoomNumber = "101" };
        var tenant = new User { Id = Guid.NewGuid(), FullName = "Test Tenant", Email = "t@t.com" };
        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            Room = room,
            MonthlyRent = 5_000_000,
            StartDate = new DateOnly(2026, 1, 1),
            MoveInDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2027, 12, 31),
            Status = ContractStatus.Active,
            ContractTenants = new List<ContractTenant>
            {
                new() { TenantUserId = tenant.Id, IsMainTenant = true, Tenant = tenant, MoveInDate = new DateOnly(2026, 1, 1) }
            }
        };

        _currentUser.Setup(m => m.GetRequiredUserId()).Returns(tenant.Id);
        _buildingScope
            .Setup(m => m.AuthorizeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _db.Setup(m => m.Buildings)
            .Returns(new List<Building> { building }.AsQueryable().BuildMockDbSet().Object);
        _db.Setup(m => m.Contracts)
            .Returns(new List<Contract> { contract }.AsQueryable().BuildMockDbSet().Object);
        _db.Setup(m => m.Invoices)
            .Returns(new List<Invoice>().AsQueryable().BuildMockDbSet().Object);
        _db.Setup(m => m.Services)
            .Returns(new List<Service>().AsQueryable().BuildMockDbSet().Object);
        _db.Setup(m => m.RoomServices)
            .Returns(new List<RoomService>().AsQueryable().BuildMockDbSet().Object);
        _db.Setup(m => m.MeterReadings)
            .Returns(new List<MeterReading>().AsQueryable().BuildMockDbSet().Object);
        _db.Setup(m => m.InvoiceDetails)
            .Returns(new List<InvoiceDetail>().AsQueryable().BuildMockDbSet().Object);
        _db.Setup(m => m.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    private async Task<DateOnly> GetDueDate(int invoiceDueDay, int billingYear, int billingMonth)
    {
        SetupMocks(invoiceDueDay, out var buildingId);
        var result = await CreateHandler().Handle(
            new GenerateInvoicesCommand { BuildingId = buildingId, BillingYear = billingYear, BillingMonth = billingMonth },
            default);
        result.Generated.Should().HaveCount(1);
        return result.Generated[0].DueDate;
    }

    // ── Ngày bình thường ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(10, 2026, 3,  2026, 4, 10)]   // tháng 3 → hạn ngày 10/4
    [InlineData(15, 2026, 5,  2026, 6, 15)]   // tháng 5 → hạn ngày 15/6
    [InlineData(1,  2026, 3,  2026, 4,  1)]   // ngày đầu tháng
    [InlineData(28, 2026, 3,  2026, 4, 28)]   // ngày 28 luôn hợp lệ
    public async Task DueDate_NormalDueDay_IsCorrectDayOfNextMonth(
        int dueDay, int billingYear, int billingMonth,
        int expectedYear, int expectedMonth, int expectedDay)
    {
        var dueDate = await GetDueDate(dueDay, billingYear, billingMonth);
        dueDate.Should().Be(new DateOnly(expectedYear, expectedMonth, expectedDay));
    }

    // ── Tháng kỳ thanh toán là tháng 12 → sang năm mới ───────────────────────

    [Theory]
    [InlineData(10, 2026, 12, 2027, 1, 10)]   // tháng 12/2026 → hạn 10/1/2027
    [InlineData(31, 2026, 12, 2027, 1, 31)]   // tháng 12 → tháng 1 có 31 ngày
    public async Task DueDate_BillingMonthIsDecember_WrapsToJanuaryNextYear(
        int dueDay, int billingYear, int billingMonth,
        int expectedYear, int expectedMonth, int expectedDay)
    {
        var dueDate = await GetDueDate(dueDay, billingYear, billingMonth);
        dueDate.Should().Be(new DateOnly(expectedYear, expectedMonth, expectedDay));
    }

    // ── InvoiceDueDay vượt số ngày của tháng thanh toán → clamp ──────────────

    [Theory]
    [InlineData(31, 2026, 1, 2026, 2, 28)]   // tháng 1 → tháng 2 không có ngày 31, clamp về 28
    [InlineData(31, 2026, 3, 2026, 4, 30)]   // tháng 3 → tháng 4 không có ngày 31, clamp về 30
    [InlineData(31, 2026, 5, 2026, 6, 30)]   // tháng 5 → tháng 6 không có ngày 31, clamp về 30
    [InlineData(30, 2026, 1, 2026, 2, 28)]   // tháng 2 không có ngày 30, clamp về 28
    [InlineData(29, 2026, 1, 2026, 2, 28)]   // năm không nhuận: tháng 2 không có ngày 29
    public async Task DueDate_DueDayExceedsNextMonthDays_ClampsToLastDay(
        int dueDay, int billingYear, int billingMonth,
        int expectedYear, int expectedMonth, int expectedDay)
    {
        var dueDate = await GetDueDate(dueDay, billingYear, billingMonth);
        dueDate.Should().Be(new DateOnly(expectedYear, expectedMonth, expectedDay));
    }

    // ── Năm nhuận: tháng 2 có 29 ngày ────────────────────────────────────────

    [Fact]
    public async Task DueDate_LeapYear_DueDayIs29_IsValidFeb29()
    {
        // 2028 là năm nhuận → tháng 2/2028 có ngày 29
        var dueDate = await GetDueDate(invoiceDueDay: 29, billingYear: 2028, billingMonth: 1);
        dueDate.Should().Be(new DateOnly(2028, 2, 29));
    }

    [Fact]
    public async Task DueDate_LeapYear_DueDayIs31_ClampsToFeb29()
    {
        // 2028 là năm nhuận → clamp 31 về 29 (không phải 28)
        var dueDate = await GetDueDate(invoiceDueDay: 31, billingYear: 2028, billingMonth: 1);
        dueDate.Should().Be(new DateOnly(2028, 2, 29));
    }

    // ── DueDay = ngày cuối kỳ hiện tại (không phải kỳ tiếp theo) ─────────────

    [Fact]
    public async Task DueDate_DueDayIs31_BillingMonthHas31Days_IsDay31OfNextMonth()
    {
        // Tháng 3 có 31 ngày, tháng 4 có 30 → clamp 31 về 30
        var dueDate = await GetDueDate(invoiceDueDay: 31, billingYear: 2026, billingMonth: 3);
        dueDate.Should().Be(new DateOnly(2026, 4, 30));
    }

    [Fact]
    public async Task DueDate_DueDayIs31_NextMonthAlsoHas31Days_IsDay31()
    {
        // Tháng 7 → tháng 8 có 31 ngày → không clamp
        var dueDate = await GetDueDate(invoiceDueDay: 31, billingYear: 2026, billingMonth: 7);
        dueDate.Should().Be(new DateOnly(2026, 8, 31));
    }

    // ── DueDate luôn thuộc tháng TIẾP THEO kỳ thanh toán ─────────────────────

    [Theory]
    [InlineData(2026, 1)]
    [InlineData(2026, 6)]
    [InlineData(2026, 12)]
    public async Task DueDate_AlwaysInMonthAfterBillingMonth(int billingYear, int billingMonth)
    {
        var dueDate = await GetDueDate(invoiceDueDay: 10, billingYear, billingMonth);

        var expectedDueMonth = new DateOnly(billingYear, billingMonth, 1).AddMonths(1);
        dueDate.Year.Should().Be(expectedDueMonth.Year);
        dueDate.Month.Should().Be(expectedDueMonth.Month);
    }
}
