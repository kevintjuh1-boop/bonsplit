using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Infrastructure.Persistence.Configurations;

public class SettlementConfiguration : IEntityTypeConfiguration<Settlement>
{
    public void Configure(EntityTypeBuilder<Settlement> builder)
    {
        builder.ToTable("Settlements");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Note).HasMaxLength(500);
        builder.Property(s => s.CreatedAt).IsRequired();

        builder.HasOne(s => s.FromPerson)
            .WithMany()
            .HasForeignKey(s => s.FromPersonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.ToPerson)
            .WithMany()
            .HasForeignKey(s => s.ToPersonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.FromPersonId);
        builder.HasIndex(s => s.ToPersonId);
        builder.HasIndex(s => s.SettlementDate);
    }
}
