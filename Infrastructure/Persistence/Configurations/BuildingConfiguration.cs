using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class BuildingConfiguration : IEntityTypeConfiguration<Building>
{
    public void Configure(EntityTypeBuilder<Building> builder)
    {
        builder.HasKey(b => b.Id);

        builder.HasQueryFilter(b => b.DeletedAt == null);

        builder.Property(b => b.Name).IsRequired().HasMaxLength(200);
        builder.Property(b => b.Address).IsRequired().HasMaxLength(500);
        builder.Property(b => b.Description).HasMaxLength(2000);

        builder.HasMany(b => b.Rooms)
            .WithOne(r => r.Building)
            .HasForeignKey(r => r.BuildingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.BuildingStaffs)
            .WithOne(bs => bs.Building)
            .HasForeignKey(bs => bs.BuildingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.Services)
            .WithOne(s => s.Building)
            .HasForeignKey(s => s.BuildingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.Expenses)
            .WithOne(e => e.Building)
            .HasForeignKey(e => e.BuildingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
