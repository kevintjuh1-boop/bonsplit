using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Infrastructure.Persistence.Configurations;

public class ExternalPaymentConfiguration : IEntityTypeConfiguration<ExternalPayment>
{
    public void Configure(EntityTypeBuilder<ExternalPayment> builder)
    {
        builder.ToTable("ExternalPayments");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.RecipientName).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Note).HasMaxLength(500);
        builder.Property(p => p.CreatedAt).IsRequired();

        builder.HasOne(p => p.OwedToPerson)
            .WithMany()
            .HasForeignKey(p => p.OwedToPersonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.OwedToPersonId);
        builder.HasIndex(p => p.RecipientName);
    }
}
