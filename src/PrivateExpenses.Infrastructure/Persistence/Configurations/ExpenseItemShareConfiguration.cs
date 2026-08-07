using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Infrastructure.Persistence.Configurations;

public class ExpenseItemShareConfiguration : IEntityTypeConfiguration<ExpenseItemShare>
{
    public void Configure(EntityTypeBuilder<ExpenseItemShare> builder)
    {
        builder.ToTable("ExpenseItemShares");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.HasOne(s => s.Person)
            .WithMany(p => p.ExpenseItemShares)
            .HasForeignKey(s => s.PersonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.ExpenseItemId, s.PersonId }).IsUnique();
        builder.HasIndex(s => s.PersonId);
    }
}
