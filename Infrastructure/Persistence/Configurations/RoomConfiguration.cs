using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.HasIndex(r => new { r.BuildingId, r.RoomCode }).IsUnique();
        builder.Property(r => r.Status).HasConversion<string>();
        
        builder.HasOne(r => r.Building)
            .WithMany(b => b.Rooms)
            .HasForeignKey(r => r.BuildingId);
    }
}
