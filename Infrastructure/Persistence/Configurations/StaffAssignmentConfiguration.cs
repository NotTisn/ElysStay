using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class StaffAssignmentConfiguration : IEntityTypeConfiguration<StaffAssignment>
{
    public void Configure(EntityTypeBuilder<StaffAssignment> builder)
    {
        builder.HasKey(sa => new { sa.BuildingId, sa.StaffId });

        builder.HasOne(sa => sa.Building)
            .WithMany(b => b.BuildingStaffs)
            .HasForeignKey(sa => sa.BuildingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sa => sa.Staff)
            .WithMany(u => u.BuildingStaffAssignments)
            .HasForeignKey(sa => sa.StaffId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}