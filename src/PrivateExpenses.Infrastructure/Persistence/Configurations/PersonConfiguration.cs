using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Infrastructure.Persistence.Configurations;

public class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("People");
        builder.HasKey(p => p.Id);
        // All entity Ids in this app are client-generated GUIDs (Guid.NewGuid() in code), never
        // database-generated. Without ValueGeneratedNever, EF's convention-based ValueGeneratedOnAdd
        // for Guid keys makes the change tracker treat a new entity discovered via navigation (rather
        // than an explicit DbSet.Add()) as "already exists" (Modified) whenever its key is non-default —
        // which silently turns inserts into failing no-op UPDATEs. Applied to every entity below.
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Name).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Initial).IsRequired().HasMaxLength(2);
        builder.Property(p => p.ColorKey).IsRequired().HasMaxLength(30);
        builder.Property(p => p.CreatedAt).IsRequired();

        builder.HasIndex(p => p.Name).IsUnique();
    }
}
