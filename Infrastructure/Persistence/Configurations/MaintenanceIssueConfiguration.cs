using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class MaintenanceIssueConfiguration : IEntityTypeConfiguration<MaintenanceIssue>
{
    public void Configure(EntityTypeBuilder<MaintenanceIssue> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Title).IsRequired().HasMaxLength(200);
        builder.Property(m => m.Description).IsRequired().HasMaxLength(5000);
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Priority).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(m => new { m.BuildingId, m.Status });
        builder.HasIndex(m => m.ReportedBy);

        builder.HasOne(m => m.Building)
            .WithMany()
            .HasForeignKey(m => m.BuildingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Room)
            .WithMany(r => r.Issues)
            .HasForeignKey(m => m.RoomId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(m => m.Reporter)
            .WithMany()
            .HasForeignKey(m => m.ReportedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Assignee)
            .WithMany()
            .HasForeignKey(m => m.AssignedTo)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
