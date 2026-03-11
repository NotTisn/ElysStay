using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Category).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Description).IsRequired().HasMaxLength(1000);
        builder.Property(e => e.ReceiptUrl).HasMaxLength(500);
        builder.Property(e => e.Amount).HasColumnType("numeric(18,2)");

        builder.HasIndex(e => new { e.BuildingId, e.ExpenseDate });

        builder.HasOne(e => e.Building)
            .WithMany(b => b.Expenses)
            .HasForeignKey(e => e.BuildingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Room)
            .WithMany()
            .HasForeignKey(e => e.RoomId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Recorder)
            .WithMany()
            .HasForeignKey(e => e.RecordedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
