using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.HasIndex(c => c.ContractCode).IsUnique();
        builder.Property(c => c.Status).HasConversion<string>();
        builder.Property(c => c.DepositStatus).HasConversion<string>();
        
        builder.HasOne(c => c.Room)
            .WithMany(r => r.Contracts)
            .HasForeignKey(c => c.RoomId);
    }
}
