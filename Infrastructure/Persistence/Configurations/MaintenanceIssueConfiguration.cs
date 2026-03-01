using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class MaintenanceIssueConfiguration : IEntityTypeConfiguration<MaintenanceIssue>
{
    public void Configure(EntityTypeBuilder<MaintenanceIssue> builder)
    {
        builder.Property(m => m.Status).HasConversion<string>();
        builder.Property(m => m.Priority).HasConversion<string>();
    }
}
