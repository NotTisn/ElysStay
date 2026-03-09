using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ContractTenantConfiguration : IEntityTypeConfiguration<ContractTenant>
{
    public void Configure(EntityTypeBuilder<ContractTenant> builder)
    {
        builder.HasKey(ct => ct.Id);

        builder.HasIndex(ct => new { ct.ContractId, ct.TenantUserId }).IsUnique();

        builder.HasOne(ct => ct.Contract)
            .WithMany(c => c.ContractTenants)
            .HasForeignKey(ct => ct.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ct => ct.Tenant)
            .WithMany()
            .HasForeignKey(ct => ct.TenantUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}