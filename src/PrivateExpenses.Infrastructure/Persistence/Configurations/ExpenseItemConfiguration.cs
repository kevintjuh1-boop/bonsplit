using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Infrastructure.Persistence.Configurations;

public class ExpenseItemConfiguration : IEntityTypeConfiguration<ExpenseItem>
{
    public void Configure(EntityTypeBuilder<ExpenseItem> builder)
    {
        builder.ToTable("ExpenseItems");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.Description).IsRequired().HasMaxLength(300);
        builder.Property(i => i.PromotionLabel).HasMaxLength(80);
        builder.Property(i => i.Quantity).HasPrecision(10, 3);
        builder.Property(i => i.CreatedAt).IsRequired();

        builder.HasMany(i => i.Shares)
            .WithOne(s => s.ExpenseItem)
            .HasForeignKey(s => s.ExpenseItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.ExpenseId);
    }
}
