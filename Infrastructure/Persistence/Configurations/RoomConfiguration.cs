using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.HasKey(r => r.Id);

        builder.HasQueryFilter(r => r.DeletedAt == null);

        builder.Property(r => r.RoomNumber).IsRequired().HasMaxLength(50);
        builder.Property(r => r.Area).HasColumnType("numeric(10,2)");
        builder.Property(r => r.Price).HasColumnType("numeric(18,2)");
        builder.Property(r => r.Description).HasMaxLength(2000);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.RowVersion).IsRowVersion();

        builder.HasIndex(r => new { r.BuildingId, r.RoomNumber }).IsUnique();
        builder.HasIndex(r => new { r.BuildingId, r.Status });

        builder.HasMany(r => r.RoomServices)
            .WithOne(rs => rs.Room)
            .HasForeignKey(rs => rs.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.Reservations)
            .WithOne(rr => rr.Room)
            .HasForeignKey(rr => rr.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Contracts)
            .WithOne(c => c.Room)
            .HasForeignKey(c => c.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.MeterReadings)
            .WithOne(m => m.Room)
            .HasForeignKey(m => m.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.Issues)
            .WithOne(i => i.Room)
            .HasForeignKey(i => i.RoomId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}