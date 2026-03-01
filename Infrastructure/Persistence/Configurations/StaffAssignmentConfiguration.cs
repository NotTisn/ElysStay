using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class StaffAssignmentConfiguration : IEntityTypeConfiguration<StaffAssignment>
{
    public void Configure(EntityTypeBuilder<StaffAssignment> builder)
    {
        builder.HasKey(sa => new { sa.StaffId, sa.BuildingId });
        
        builder.HasOne(sa => sa.Staff)
            .WithMany(u => u.ManagedBuildings)
            .HasForeignKey(sa => sa.StaffId);
            
        builder.HasOne(sa => sa.Building)
            .WithMany(b => b.StaffAssignments)
            .HasForeignKey(sa => sa.BuildingId);
    }
}
