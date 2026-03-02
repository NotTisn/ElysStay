using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class InvoiceDetailConfiguration : IEntityTypeConfiguration<InvoiceDetail>
{
    public void Configure(EntityTypeBuilder<InvoiceDetail> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Description).IsRequired();
        builder.Property(d => d.UnitPrice).HasColumnType("numeric(18,2)");
        builder.Property(d => d.Quantity).HasColumnType("numeric(18,3)");
        builder.Property(d => d.Amount).HasColumnType("numeric(18,2)");
        builder.Property(d => d.PreviousReading).HasColumnType("numeric(18,3)");
        builder.Property(d => d.CurrentReading).HasColumnType("numeric(18,3)");

        builder.HasOne(d => d.Invoice)
            .WithMany(i => i.InvoiceDetails)
            .HasForeignKey(d => d.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Service)
            .WithMany(s => s.InvoiceDetails)
            .HasForeignKey(d => d.ServiceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
