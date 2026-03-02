using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class MeterReadingConfiguration : IEntityTypeConfiguration<MeterReading>
{
    public void Configure(EntityTypeBuilder<MeterReading> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.PreviousReading).HasColumnType("numeric(18,3)");
        builder.Property(m => m.CurrentReading).HasColumnType("numeric(18,3)");
        builder.Property(m => m.Consumption).HasColumnType("numeric(18,3)");

        builder.HasIndex(m => new { m.RoomId, m.ServiceId, m.BillingYear, m.BillingMonth }).IsUnique();

        builder.HasOne(m => m.Room)
            .WithMany(r => r.MeterReadings)
            .HasForeignKey(m => m.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Service)
            .WithMany(s => s.MeterReadings)
            .HasForeignKey(m => m.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Creator)
            .WithMany()
            .HasForeignKey(m => m.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
