using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class TenantProfileConfiguration : IEntityTypeConfiguration<TenantProfile>
{
    public void Configure(EntityTypeBuilder<TenantProfile> builder)
    {
        builder.HasKey(tp => tp.Id);

        builder.Property(tp => tp.IdNumber).HasMaxLength(20);
        builder.Property(tp => tp.IdFrontUrl).HasMaxLength(500);
        builder.Property(tp => tp.IdBackUrl).HasMaxLength(500);
        builder.Property(tp => tp.Gender).HasMaxLength(10);
        builder.Property(tp => tp.PermanentAddress).HasMaxLength(500);
        builder.Property(tp => tp.IssuedPlace).HasMaxLength(200);

        builder.HasIndex(tp => tp.UserId).IsUnique();
        builder.HasIndex(tp => tp.IdNumber).IsUnique().HasFilter("\"IdNumber\" IS NOT NULL");

        builder.HasOne(tp => tp.User)
            .WithOne(u => u.TenantProfile)
            .HasForeignKey<TenantProfile>(tp => tp.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}