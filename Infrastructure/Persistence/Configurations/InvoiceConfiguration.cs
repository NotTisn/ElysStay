using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.RentAmount).HasColumnType("numeric(18,2)");
        builder.Property(i => i.ServiceAmount).HasColumnType("numeric(18,2)");
        builder.Property(i => i.PenaltyAmount).HasColumnType("numeric(18,2)");
        builder.Property(i => i.DiscountAmount).HasColumnType("numeric(18,2)");
        builder.Property(i => i.TotalAmount).HasColumnType("numeric(18,2)");
        builder.Property(i => i.Status).HasConversion<string>();

        builder.HasIndex(i => new { i.ContractId, i.BillingYear, i.BillingMonth }).IsUnique();

        builder.HasOne(i => i.Contract)
            .WithMany(c => c.Invoices)
            .HasForeignKey(i => i.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Creator)
            .WithMany()
            .HasForeignKey(i => i.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.InvoiceDetails)
            .WithOne(d => d.Invoice)
            .HasForeignKey(d => d.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.Payments)
            .WithOne(p => p.Invoice)
            .HasForeignKey(p => p.InvoiceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
