using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class TenantProfileConfiguration : IEntityTypeConfiguration<TenantProfile>
{
    public void Configure(EntityTypeBuilder<TenantProfile> builder)
    {
        builder.HasKey(tp => tp.Id);

        builder.HasIndex(tp => tp.UserId).IsUnique();
        builder.HasIndex(tp => tp.IdentityCard).IsUnique();

        builder.HasOne(tp => tp.User)
            .WithOne(u => u.TenantProfile)
            .HasForeignKey<TenantProfile>(tp => tp.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}