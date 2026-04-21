using Xunit;
using FluentAssertions;
using Domain.Entities;
using System;

namespace ElysStay.Tests.Unit.Business;

public class ExpenseUnitTests
{
    [Fact]
    public void NewExpense_ShouldRecordDatesAndAmount_Correctly()
    {
        // Arrange
        var expense = new Expense
        {
            BuildingId = Guid.NewGuid(),
            Category = "Cleaning",
            Amount = 500_000,
            ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        // Act
        // Typically validation/business logic happens here

        // Assert
        expense.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        expense.Amount.Should().Be(500_000);
        expense.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void SoftDeleteExpense_ShouldSetDeletedAt()
    {
        // Arrange
        var expense = new Expense
        {
            BuildingId = Guid.NewGuid(),
            Amount = 100_000
        };

        // Act
        expense.DeletedAt = DateTime.UtcNow;

        // Assert
        expense.DeletedAt.Should().NotBeNull();
        expense.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }
}
