using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    // Chỉ giữ lại khai báo DbSet
    public DbSet<User> Users => Set<User>();
    public DbSet<TenantProfile> TenantProfiles => Set<TenantProfile>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<StaffAssignment> StaffAssignments => Set<StaffAssignment>();
    public DbSet<RoomReservation> RoomReservations => Set<RoomReservation>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractTenant> ContractTenants => Set<ContractTenant>();
    public DbSet<MeterReading> MeterReadings => Set<MeterReading>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<MaintenanceIssue> MaintenanceIssues => Set<MaintenanceIssue>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Dòng code ma thuật gom toàn bộ 14 file Configuration lại
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}