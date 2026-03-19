namespace Application.Features.Expenses.DTOs;

public record ExpenseSummaryDto(
    decimal TotalAmount,
    int ExpenseCount);