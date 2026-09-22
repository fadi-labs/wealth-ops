using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WealthOps.Domain.Entities;
using WealthOps.Domain.ValueObjects;

namespace WealthOps.Infrastructure.Persistence.Configurations;

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.AbsolutePath)
            .IsRequired();

        builder.Property(d => d.DocumentType)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(d => d.Corpus)
            .HasConversion<string>()
            .IsRequired();

        // Stored as its hex text rather than bytea: it is read by humans in diagnostics far more
        // often than it is compared in bulk, and 64 chars at this volume costs nothing (NFR-6).
        builder.Property(d => d.ContentHash)
            .HasConversion(
                hash => hash.Value,
                value => ContentHash.Parse(value))
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(d => d.TaxpayerId);

        builder.Property(d => d.TaxYear)
            .HasConversion(
                taxYear => taxYear!.Value.Value,
                value => TaxYear.Create(value));

        builder.Property(d => d.IngestedAtUtc)
            .IsRequired();

        // The guarantee behind BR-11/AC-1: the same content cannot be recorded twice, enforced by
        // the database rather than by the store remembering to check.
        builder.HasIndex(d => d.ContentHash)
            .IsUnique()
            .HasDatabaseName("ix_documents_content_hash");

        builder.HasIndex(d => d.Corpus)
            .HasDatabaseName("ix_documents_corpus");
    }
}
