using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Infrastructure.Persistence.Configurations;

public class ExpensePaymentConfiguration : IEntityTypeConfiguration<ExpensePayment>
{
    public void Configure(EntityTypeBuilder<ExpensePayment> builder)
    {
        builder.ToTable("ExpensePayments");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.HasOne(p => p.Person)
            .WithMany(person => person.ExpensePayments)
            .HasForeignKey(p => p.PersonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.ExpenseId, p.PersonId }).IsUnique();
        builder.HasIndex(p => p.PersonId);
    }
}
