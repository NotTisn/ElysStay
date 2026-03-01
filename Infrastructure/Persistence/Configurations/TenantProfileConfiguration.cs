using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class TenantProfileConfiguration : IEntityTypeConfiguration<TenantProfile>
{
    public void Configure(EntityTypeBuilder<TenantProfile> builder)
    {
        builder.HasKey(t => t.UserId);
        builder.HasIndex(t => t.IdentityCard).IsUnique();
        
        builder.HasOne(t => t.User)
            .WithOne(u => u.TenantProfile)
            .HasForeignKey<TenantProfile>(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
