using Application.Features.Invoices.Commands;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Xunit;

namespace ElysStay.Tests.Unit.Business;

/// <summary>
/// Unit tests for CalculateRentAmount proration logic.
/// Maps to @Rent scenarios in InvoiceCalculation.feature.
/// Uses internal access granted via InternalsVisibleTo in Application.csproj.
/// </summary>
public class InvoiceRentProrationUnitTests
{
    // March 2026: 31 days, rent = 5,000,000 VND
    private static readonly DateOnly PeriodStart = new(2026, 3, 1);
    private static readonly DateOnly PeriodEnd   = new(2026, 3, 31);
    private const int DaysInMonth   = 31;
    private const decimal MonthlyRent = 5_000_000;

    private static decimal Calculate(DateOnly moveInDate, DateOnly? terminationDate = null)
    {
        var contract = new Contract
        {
            MonthlyRent     = MonthlyRent,
            StartDate       = new DateOnly(2026, 1, 1), // before period — no skip
            MoveInDate      = moveInDate,
            TerminationDate = terminationDate,
            Status          = ContractStatus.Active
        };
        return GenerateInvoicesCommandHandler.CalculateRentAmount(
            contract, PeriodStart, PeriodEnd, DaysInMonth);
    }

    // ── @Rent: Calculate full monthly rent ────────────────────────────────────

    [Fact]
    public void CalculateRentAmount_FullMonthContract_ReturnsFullRent()
    {
        // Contract active for entire March 2026 (MoveInDate = 2026-01-01, no termination)
        // ActiveDays = 31, RentAmount = 5,000,000
        var result = Calculate(moveInDate: new DateOnly(2026, 1, 1));

        result.Should().Be(5_000_000);
    }

    // ── @Rent: Calculate prorated rent for first month (PR-05) ───────────────

    [Fact]
    public void CalculateRentAmount_ContractStartsMidMonth_ProratesCorrectly()
    {
        // Contract starts on 2026-03-16 → MoveInDate = 2026-03-16
        // ActiveDays = 31 - 16 + 1 = 16
        // RentAmount = Round(5,000,000 / 31 * 16) = 2,580,645
        var result = Calculate(moveInDate: new DateOnly(2026, 3, 16));

        result.Should().Be(2_580_645);
    }

    // ── @Rent: Calculate prorated rent for last month (PR-06) ────────────────

    [Fact]
    public void CalculateRentAmount_ContractEndsMidMonth_ProratesCorrectly()
    {
        // Contract ends on 2026-03-10 → TerminationDate = 2026-03-10
        // ActiveDays = 10 - 1 + 1 = 10
        // RentAmount = Round(5,000,000 / 31 * 10) = 1,612,903
        var result = Calculate(
            moveInDate:       new DateOnly(2026, 1, 1),
            terminationDate:  new DateOnly(2026, 3, 10));

        result.Should().Be(1_612_903);
    }

    // ── @Rent: Contract starts and ends in same month ─────────────────────────

    [Fact]
    public void CalculateRentAmount_ContractStartsAndEndsInSameMonth_ProratesCorrectly()
    {
        // Starts 2026-03-10, ends 2026-03-15
        // ActiveDays = 15 - 10 + 1 = 6
        // RentAmount = Round(5,000,000 / 31 * 6) = 967,742
        var result = Calculate(
            moveInDate:       new DateOnly(2026, 3, 10),
            terminationDate:  new DateOnly(2026, 3, 15));

        result.Should().Be(967_742);
    }

    // ── @Rent: Full rent when contract spans multiple months ──────────────────

    [Fact]
    public void CalculateRentAmount_ContractSpansMultipleMonths_FullRentForMiddleMonth()
    {
        // Starts 2026-02-15, ends 2026-04-20 → March is a full middle month
        // MoveInDate(02-15) is NOT after PeriodStart(03-01) → no start proration
        // TerminationDate(04-20) is NOT before PeriodEnd(03-31) → no end proration
        // RentAmount = 5,000,000 (full rent, not prorated)
        var result = Calculate(
            moveInDate:       new DateOnly(2026, 2, 15),
            terminationDate:  new DateOnly(2026, 4, 20));

        result.Should().Be(5_000_000);
    }

    // ── @Rent: No off-by-one for full month (03-01 to 03-31) ─────────────────

    [Fact]
    public void CalculateRentAmount_ContractActiveExactFullMonth_NoOffByOne()
    {
        // MoveInDate = 03-01 (same as PeriodStart → condition > is false → no proration)
        // TerminationDate = 03-31 (same as PeriodEnd → condition < is false → no proration)
        // effectiveStart == periodStart && effectiveEnd == periodEnd → full rent
        var result = Calculate(
            moveInDate:       new DateOnly(2026, 3, 1),
            terminationDate:  new DateOnly(2026, 3, 31));

        result.Should().Be(5_000_000);
    }

    // ── Edge case: termination on last day does NOT prorate ───────────────────

    [Fact]
    public void CalculateRentAmount_TerminationOnLastDayOfMonth_ReturnsFullRent()
    {
        // TerminationDate = 03-31 = periodEnd → condition is TerminationDate < periodEnd → false
        // So no end-proration → full rent
        var result = Calculate(
            moveInDate:       new DateOnly(2026, 1, 1),
            terminationDate:  new DateOnly(2026, 3, 31));

        result.Should().Be(5_000_000);
    }

    // ── Edge case: zero active days → zero rent ───────────────────────────────

    [Fact]
    public void CalculateRentAmount_ZeroActiveDays_ReturnsZero()
    {
        // MoveInDate = 03-16, TerminationDate = 03-15 → effectiveEnd < effectiveStart
        // days = Max(0, ...) = 0 → rent = 0
        var result = Calculate(
            moveInDate:       new DateOnly(2026, 3, 16),
            terminationDate:  new DateOnly(2026, 3, 15));

        result.Should().Be(0);
    }
}
