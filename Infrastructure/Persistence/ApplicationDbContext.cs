using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<TenantProfile> TenantProfiles => Set<TenantProfile>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<StaffAssignment> StaffAssignments => Set<StaffAssignment>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<RoomService> RoomServices => Set<RoomService>();
    public DbSet<RoomReservation> RoomReservations => Set<RoomReservation>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractTenant> ContractTenants => Set<ContractTenant>();
    public DbSet<MeterReading> MeterReadings => Set<MeterReading>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceDetail> InvoiceDetails => Set<InvoiceDetail>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<MaintenanceIssue> MaintenanceIssues => Set<MaintenanceIssue>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Modified)
            {
                var updatedAtProp = entry.Properties
                    .FirstOrDefault(p => p.Metadata.Name == "UpdatedAt");

                if (updatedAtProp is not null && updatedAtProp.Metadata.ClrType == typeof(DateTime))
                {
                    updatedAtProp.CurrentValue = now;
                }
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}