using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.Message).IsRequired().HasMaxLength(500);
        builder.Property(n => n.CreatedAt).IsRequired();

        builder.HasOne<Expense>().WithMany().HasForeignKey(n => n.ExpenseId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Person>().WithMany().HasForeignKey(n => n.RecipientPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(n => n.ActorPersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => new { n.RecipientPersonId, n.IsRead });
        builder.HasIndex(n => n.CreatedAt);
    }
}
