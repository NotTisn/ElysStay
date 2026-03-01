using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class MeterReadingConfiguration : IEntityTypeConfiguration<MeterReading>
{
    public void Configure(EntityTypeBuilder<MeterReading> builder)
    {
        builder.HasIndex(m => new { m.ContractId, m.BillingMonth, m.BillingYear }).IsUnique();
    }
}
