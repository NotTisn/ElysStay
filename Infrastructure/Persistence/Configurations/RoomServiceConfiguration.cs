using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class RoomServiceConfiguration : IEntityTypeConfiguration<RoomService>
{
    public void Configure(EntityTypeBuilder<RoomService> builder)
    {
        builder.HasKey(rs => rs.Id);

        builder.HasIndex(rs => new { rs.RoomId, rs.ServiceId }).IsUnique();

        builder.Property(rs => rs.OverrideUnitPrice).HasColumnType("numeric(18,2)");
        builder.Property(rs => rs.OverrideQuantity).HasColumnType("numeric(18,3)");

        builder.HasOne(rs => rs.Room)
            .WithMany(r => r.RoomServices)
            .HasForeignKey(rs => rs.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rs => rs.Service)
            .WithMany(s => s.RoomServices)
            .HasForeignKey(rs => rs.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
