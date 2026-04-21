using Xunit;
using FluentAssertions;
using System.Collections.Generic;
using Domain.Entities;
using System.Linq;

namespace ElysStay.Tests.Unit.Business;

public class DashboardAnalyticsUnitTests
{
    [Fact]
    public void CalculateRevenue_SumPaidInvoices_ReturnsCorrectTotal()
    {
        // Arrange
        var invoices = new List<Invoice>
        {
            new Invoice { RentAmount = 5_000_000, Status = Domain.Enums.InvoiceStatus.Paid },
            new Invoice { RentAmount = 4_000_000, Status = Domain.Enums.InvoiceStatus.Paid },
            new Invoice { RentAmount = 6_000_000, Status = Domain.Enums.InvoiceStatus.Overdue } // Should be ignored
        };

        // Act
        var revenue = invoices
            .Where(i => i.Status == Domain.Enums.InvoiceStatus.Paid)
            .Sum(i => i.RentAmount);

        // Assert
        revenue.Should().Be(9_000_000);
    }

    [Fact]
    public void CalculateExpenses_SumExpenses_ReturnsCorrectTotal()
    {
        // Arrange
        var expenses = new List<Expense>
        {
            new Expense { Amount = 500_000, DeletedAt = null },
            new Expense { Amount = 200_000, DeletedAt = null },
            new Expense { Amount = 1_000_000, DeletedAt = System.DateTime.UtcNow } // Soft deleted, should be ignored
        };

        // Act
        var totalExpenses = expenses
            .Where(e => e.DeletedAt == null)
            .Sum(e => e.Amount);

        // Assert
        totalExpenses.Should().Be(700_000);
    }

    [Fact]
    public void CalculateOccupancyRate_RatioOfOccupiedToTotal_ReturnsCorrectPercentage()
    {
        // Arrange
        var rooms = new List<Room>
        {
            new Room { Status = Domain.Enums.RoomStatus.Occupied },
            new Room { Status = Domain.Enums.RoomStatus.Occupied },
            new Room { Status = Domain.Enums.RoomStatus.Available },
            new Room { Status = Domain.Enums.RoomStatus.Maintenance }
        };

        // Act
        int totalRooms = rooms.Count;
        int occupiedRooms = rooms.Count(r => r.Status == Domain.Enums.RoomStatus.Occupied);
        double occupancyRate = totalRooms == 0 ? 0 : ((double)occupiedRooms / totalRooms) * 100;

        // Assert
        occupancyRate.Should().Be(50.0);
    }
}
