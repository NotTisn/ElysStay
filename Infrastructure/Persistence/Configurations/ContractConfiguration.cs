using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.MonthlyRent).HasColumnType("numeric(18,2)");
        builder.Property(c => c.DepositAmount).HasColumnType("numeric(18,2)");
        builder.Property(c => c.RefundAmount).HasColumnType("numeric(18,2)");
        builder.Property(c => c.DepositStatus).HasConversion<string>();
        builder.Property(c => c.Status).HasConversion<string>();

        builder.HasOne(c => c.Room)
            .WithMany(r => r.Contracts)
            .HasForeignKey(c => c.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.TenantUser)
            .WithMany()
            .HasForeignKey(c => c.TenantUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Reservation)
            .WithMany(r => r.Contracts)
            .HasForeignKey(c => c.ReservationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.Creator)
            .WithMany()
            .HasForeignKey(c => c.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.ContractTenants)
            .WithOne(ct => ct.Contract)
            .HasForeignKey(ct => ct.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Invoices)
            .WithOne(i => i.Contract)
            .HasForeignKey(i => i.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Payments)
            .WithOne(p => p.Contract)
            .HasForeignKey(p => p.ContractId)
            .OnDelete(DeleteBehavior.SetNull);

        // Prevent duplicate active contracts per room (race condition guard)
        builder.HasIndex(c => c.RoomId)
            .HasFilter("\"Status\" = 'Active'")
            .IsUnique()
            .HasDatabaseName("IX_Contracts_RoomId_Active");

        // Hot query paths
        builder.HasIndex(c => new { c.RoomId, c.Status });
        builder.HasIndex(c => new { c.TenantUserId, c.Status });
        builder.HasIndex(c => c.EndDate);

        // String length constraints
        builder.Property(c => c.TerminationNote).HasMaxLength(1000);
        builder.Property(c => c.Note).HasMaxLength(1000);
        builder.Property(c => c.DepositStatus).HasMaxLength(30);
        builder.Property(c => c.Status).HasMaxLength(30);
    }
}