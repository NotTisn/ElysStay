using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class RoomReservationConfiguration : IEntityTypeConfiguration<RoomReservation>
{
    public void Configure(EntityTypeBuilder<RoomReservation> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.DepositAmount).HasColumnType("numeric(18,2)");
        builder.Property(r => r.RefundAmount).HasColumnType("numeric(18,2)");
        builder.Property(r => r.Status).HasConversion<string>();

        builder.HasOne(r => r.Room)
            .WithMany(ro => ro.Reservations)
            .HasForeignKey(r => r.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.TenantUser)
            .WithMany()
            .HasForeignKey(r => r.TenantUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
