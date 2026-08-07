using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Infrastructure.Persistence.Configurations;

public class ReceiptDocumentConfiguration : IEntityTypeConfiguration<ReceiptDocument>
{
    public void Configure(EntityTypeBuilder<ReceiptDocument> builder)
    {
        builder.ToTable("ReceiptDocuments");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.OriginalFileName).IsRequired().HasMaxLength(260);
        builder.Property(d => d.StoredFileName).IsRequired().HasMaxLength(260);
        builder.Property(d => d.MimeType).IsRequired().HasMaxLength(100);
        builder.Property(d => d.FileHash).IsRequired().HasMaxLength(64);
        builder.Property(d => d.ParsingError).HasMaxLength(2000);
        builder.Property(d => d.UploadedAt).IsRequired();
        builder.Property(d => d.CreatedAt).IsRequired();

        builder.HasIndex(d => d.StoredFileName).IsUnique();
        builder.HasIndex(d => d.FileHash);
        builder.HasIndex(d => d.ExpenseId);
    }
}
