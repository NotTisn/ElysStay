using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Amount).HasColumnType("numeric(18,2)");
        builder.Property(p => p.Type).HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(p => p.PaymentMethod).HasMaxLength(50);
        builder.Property(p => p.ReferenceCode).HasMaxLength(100);
        builder.Property(p => p.Note).HasMaxLength(1000);

        builder.HasOne(p => p.Invoice)
            .WithMany(i => i.Payments)
            .HasForeignKey(p => p.InvoiceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(p => p.Contract)
            .WithMany(c => c.Payments)
            .HasForeignKey(p => p.ContractId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(p => p.Recorder)
            .WithMany()
            .HasForeignKey(p => p.RecordedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
