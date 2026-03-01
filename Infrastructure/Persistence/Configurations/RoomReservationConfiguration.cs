using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class RoomReservationConfiguration : IEntityTypeConfiguration<RoomReservation>
{
    public void Configure(EntityTypeBuilder<RoomReservation> builder)
    {
        builder.Property(r => r.Status).HasConversion<string>();
    }
}
