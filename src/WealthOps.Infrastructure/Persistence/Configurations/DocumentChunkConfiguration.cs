using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector;
using WealthOps.Domain.Entities;
using WealthOps.Domain.ValueObjects;

namespace WealthOps.Infrastructure.Persistence.Configurations;

internal sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.ToTable("document_chunks");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.DocumentId).IsRequired();

        builder.Property(c => c.Corpus)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(c => c.Ordinal).IsRequired();

        builder.Property(c => c.Text).IsRequired();

        builder.Property(c => c.EmbeddingModelId).IsRequired();

        builder.Property(c => c.Dimensions).IsRequired();

        // "vector" with no width — the dimension belongs to the data, not the schema (ADR-002).
        // The conversion is what keeps Pgvector out of Domain: EmbeddingVector is a plain float
        // array there, and becomes a Vector only at this boundary.
        builder.Property(c => c.Embedding)
            .HasColumnType("vector")
            .HasConversion(
                embedding => new Vector(embedding.ToArray()),
                vector => EmbeddingVector.Create(vector.ToArray()))
            .IsRequired();

        builder.HasOne<Document>()
            .WithMany()
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // The upsert key. Re-chunking a changed document replaces its chunks in place rather than
        // accumulating a second set alongside the first.
        builder.HasIndex(c => new { c.DocumentId, c.Ordinal })
            .IsUnique()
            .HasDatabaseName("ix_document_chunks_document_ordinal");

        // Every similarity search filters on this first (FR-M1-3).
        builder.HasIndex(c => c.Corpus)
            .HasDatabaseName("ix_document_chunks_corpus");
    }
}
