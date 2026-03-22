using Xunit;
using FluentAssertions;
using Domain.Entities;
using Domain.Enums;
using Tests.Integration.Fixtures;
using Tests.Integration.Builders;

namespace ElysStay.Tests.Integration.Features;

public class ExpenseIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();
    private User _owner = null!;
    private Building _building = null!;

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    private async Task SetupTestData()
    {
        _owner = TestDataBuilder.CreateUser(role: UserRole.Owner);
        _building = TestDataBuilder.CreateBuilding(_owner.Id);

        await _fixture.DbContext.Users.AddAsync(_owner);
        await _fixture.DbContext.Buildings.AddAsync(_building);
        await _fixture.DbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateExpense_WithValidData_CreatesSuccessfully()
    {
        // Arrange
        await SetupTestData();
        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            BuildingId = _building.Id,
            Category = "Maintenance",
            Description = "Roof repair",
            Amount = 5_000_000,
            ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow),
            RecordedBy = _owner.Id
        };

        // Act
        await _fixture.DbContext.Expenses.AddAsync(expense);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        var saved = _fixture.DbContext.Expenses.FirstOrDefault(e => e.Id == expense.Id);
        saved.Should().NotBeNull();
        saved!.Category.Should().Be("Maintenance");
    }

    [Fact]
    public async Task GetExpenses_FiltersByBuilding_ReturnsOnlyBuildingExpenses()
    {
        // Arrange
        await SetupTestData();
        var expense1 = new Expense
        {
            Id = Guid.NewGuid(),
            BuildingId = _building.Id,
            Category = "Maintenance",
            Amount = 1_000_000,
            ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow),
            RecordedBy = _owner.Id
        };

        var expense2 = new Expense
        {
            Id = Guid.NewGuid(),
            BuildingId = _building.Id,
            Category = "Utilities",
            Amount = 2_000_000,
            ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow),
            RecordedBy = _owner.Id
        };

        await _fixture.DbContext.Expenses.AddAsync(expense1);
        await _fixture.DbContext.Expenses.AddAsync(expense2);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var expenses = _fixture.DbContext.Expenses
            .Where(e => e.BuildingId == _building.Id)
            .ToList();

        // Assert
        expenses.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetExpenses_FiltersByCategory_ReturnsOnlyCategoryExpenses()
    {
        // Arrange
        await SetupTestData();
        var maintenance = new Expense
        {
            Id = Guid.NewGuid(),
            BuildingId = _building.Id,
            Category = "Maintenance",
            Amount = 1_000_000,
            ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow),
            RecordedBy = _owner.Id
        };

        var utilities = new Expense
        {
            Id = Guid.NewGuid(),
            BuildingId = _building.Id,
            Category = "Utilities",
            Amount = 2_000_000,
            ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow),
            RecordedBy = _owner.Id
        };

        await _fixture.DbContext.Expenses.AddAsync(maintenance);
        await _fixture.DbContext.Expenses.AddAsync(utilities);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var maintenanceExpenses = _fixture.DbContext.Expenses
            .Where(e => e.Category == "Maintenance")
            .ToList();

        // Assert
        maintenanceExpenses.Should().HaveCount(1);
    }
}
