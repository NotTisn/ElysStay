using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).IsRequired().HasMaxLength(100);
        builder.Property(s => s.Unit).IsRequired().HasMaxLength(50);
        builder.Property(s => s.UnitPrice).HasColumnType("numeric(18,2)");
        builder.Property(s => s.PreviousUnitPrice).HasColumnType("numeric(18,2)");

        builder.HasMany(s => s.RoomServices)
            .WithOne(rs => rs.Service)
            .HasForeignKey(rs => rs.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.MeterReadings)
            .WithOne(m => m.Service)
            .HasForeignKey(m => m.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.InvoiceDetails)
            .WithOne(d => d.Service)
            .HasForeignKey(d => d.ServiceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
