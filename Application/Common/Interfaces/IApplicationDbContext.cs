using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Application.Common.Interfaces;

/// <summary>
/// Abstraction over the EF Core DbContext.
/// Application layer depends on this interface; Infrastructure implements it.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<TenantProfile> TenantProfiles { get; }
    DbSet<Building> Buildings { get; }
    DbSet<StaffAssignment> StaffAssignments { get; }
    DbSet<Room> Rooms { get; }
    DbSet<Service> Services { get; }
    DbSet<RoomService> RoomServices { get; }
    DbSet<RoomReservation> RoomReservations { get; }
    DbSet<Contract> Contracts { get; }
    DbSet<ContractTenant> ContractTenants { get; }
    DbSet<MeterReading> MeterReadings { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceDetail> InvoiceDetails { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Expense> Expenses { get; }
    DbSet<MaintenanceIssue> MaintenanceIssues { get; }
    DbSet<Notification> Notifications { get; }

    /// <summary>Provides access to database-related metadata and operations (transactions, migrations, etc.).</summary>
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
