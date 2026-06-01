using Application.Features.Invoices.Commands;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Xunit;

namespace ElysStay.Tests.Unit.Business;

public class InvoiceRentProrationUnitTests
{
    private static readonly DateOnly PeriodStart = new(2026, 3, 1);
    private static readonly DateOnly PeriodEnd   = new(2026, 3, 31);
    private const decimal MonthlyRent = 5_000_000;

    private static decimal Calculate(DateOnly MoveInDate, DateOnly? TerminationDate = null)
    {
        var contract = new Contract
        {
            MonthlyRent     = MonthlyRent,
            MoveInDate      = MoveInDate,
            TerminationDate = TerminationDate,
            Status          = ContractStatus.Active
        };
        return InvoiceBuilder.CalculateRentAmount(
            contract, PeriodStart, PeriodEnd);
    }

    // ── @Rent: Calculate full monthly rent ────────────────────────────────────

    [Fact]
    public void CalculateRentAmount_FullMonthContract_ReturnsFullRent()
    {
        var result = Calculate(MoveInDate: new DateOnly(2026, 1, 1));
        result.Should().Be(5_000_000);
    }

    // ── @Rent: Calculate prorated rent for first month (PR-05) ───────────────

    [Fact]
    public void CalculateRentAmount_ContractStartsMidMonth_ProratesCorrectly()
    {
        var result = Calculate(MoveInDate: new DateOnly(2026, 3, 16));
        result.Should().Be(2_580_645);
    }

    // ── @Rent: Calculate prorated rent for last month (PR-06) ────────────────

    [Fact]
    public void CalculateRentAmount_ContractEndsMidMonth_ProratesCorrectly()
    {
        var result = Calculate(
            MoveInDate:       new DateOnly(2026, 1, 1),
            TerminationDate:  new DateOnly(2026, 3, 10));
        result.Should().Be(1_612_903);
    }

    // ── @Rent: Contract starts and ends in same month ─────────────────────────

    [Fact]
    public void CalculateRentAmount_ContractStartsAndEndsInSameMonth_ProratesCorrectly()
    {
        var result = Calculate(
            MoveInDate:       new DateOnly(2026, 3, 10),
            TerminationDate:  new DateOnly(2026, 3, 15));
        result.Should().Be(967_742);
    }

    // ── @Rent: Full rent when contract spans multiple months ──────────────────

    [Fact]
    public void CalculateRentAmount_ContractSpansMultipleMonths_FullRentForMiddleMonth()
    {
        var result = Calculate(
            MoveInDate:       new DateOnly(2026, 2, 15),
            TerminationDate:  new DateOnly(2026, 4, 20));

        result.Should().Be(5_000_000);
    }

    // ── @Rent: No off-by-one for full month (03-01 to 03-31) ─────────────────

    [Fact]
    public void CalculateRentAmount_ContractActiveExactFullMonth()
    {
        var result = Calculate(
            MoveInDate:       new DateOnly(2026, 3, 1),
            TerminationDate:  new DateOnly(2026, 3, 31));

        result.Should().Be(5_000_000);
    }

    // ── Edge case: termination on last day does NOT prorate ───────────────────

    [Fact]
    public void CalculateRentAmount_TerminationOnLastDayOfMonth_ReturnsFullRent()
    {
        var result = Calculate(
            MoveInDate:       new DateOnly(2026, 1, 1),
            TerminationDate:  new DateOnly(2026, 3, 31));

        result.Should().Be(5_000_000);
    }

    // ── Edge case: zero active days → zero rent ───────────────────────────────

    [Fact]
    public void CalculateRentAmount_ZeroActiveDays_ReturnsZero()
    {
        var result = Calculate(
            MoveInDate:       new DateOnly(2026, 3, 16),
            TerminationDate:  new DateOnly(2026, 3, 15));
        result.Should().Be(0);
    }
}
