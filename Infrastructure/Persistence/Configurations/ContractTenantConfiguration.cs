using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ContractTenantConfiguration : IEntityTypeConfiguration<ContractTenant>
{
    public void Configure(EntityTypeBuilder<ContractTenant> builder)
    {
        builder.HasKey(ct => new { ct.ContractId, ct.TenantId });
        
        builder.HasOne(ct => ct.Contract)
            .WithMany(c => c.ContractTenants)
            .HasForeignKey(ct => ct.ContractId);
    }
}
