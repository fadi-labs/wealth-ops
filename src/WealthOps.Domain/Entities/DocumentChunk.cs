using WealthOps.Domain.Enums;
using WealthOps.Domain.ValueObjects;

namespace WealthOps.Domain.Entities;

/// <summary>
/// One embedded passage of a <see cref="Document"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Corpus"/> is copied down from the parent document rather than joined at query time.
/// That duplication is deliberate: corpus-scoped search (FR-M1-3) is the mechanism that keeps
/// personal content from surfacing in a rules answer, and a filter that depends on a join is a
/// filter that can be forgotten. Here it is a column on the row being scanned.
/// </para>
/// <para>
/// <see cref="EmbeddingModelId"/> and <see cref="Dimensions"/> are stored per chunk because the
/// embedding model is configuration (ADR-002). Vectors from different models are not comparable,
/// and recording which model produced each one is what makes that state detectable instead of
/// silently wrong.
/// </para>
/// </remarks>
public sealed class DocumentChunk
{
    private DocumentChunk(
        Guid id,
        Guid documentId,
        Corpus corpus,
        int ordinal,
        string text,
        EmbeddingVector embedding,
        string embeddingModelId)
    {
        Id = id;
        DocumentId = documentId;
        Corpus = corpus;
        Ordinal = ordinal;
        Text = text;
        Embedding = embedding;
        EmbeddingModelId = embeddingModelId;
        Dimensions = embedding.Dimensions;
    }

    /// <summary>Required by EF Core materialisation. Not for application use.</summary>
    private DocumentChunk()
    {
        Text = string.Empty;
        EmbeddingModelId = string.Empty;
        Embedding = null!;
    }

    public Guid Id { get; private set; }

    public Guid DocumentId { get; private set; }

    /// <summary>Always equal to the parent document's corpus. See the remarks on this type.</summary>
    public Corpus Corpus { get; private set; }

    /// <summary>Zero-based position within the document, so a passage can be located and cited.</summary>
    public int Ordinal { get; private set; }

    public string Text { get; private set; }

    public EmbeddingVector Embedding { get; private set; }

    /// <summary>The configured model that produced <see cref="Embedding"/> (FR-M1-6).</summary>
    public string EmbeddingModelId { get; private set; }

    /// <summary>The width of <see cref="Embedding"/>, stored so a mismatch is queryable.</summary>
    public int Dimensions { get; private set; }

    /// <summary>
    /// Creates a chunk against its parent document, taking corpus and document id from it.
    /// </summary>
    /// <remarks>
    /// Taking the <see cref="Document"/> rather than a loose <see cref="Guid"/> and
    /// <see cref="Enums.Corpus"/> is what makes "a chunk's corpus equals its document's corpus" an
    /// invariant rather than a convention — there is no overload that lets a caller pass a
    /// mismatched pair.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The ordinal is negative.</exception>
    /// <exception cref="ArgumentException">The text or the model identifier is blank.</exception>
    public static DocumentChunk Create(
        Document document,
        int ordinal,
        string text,
        EmbeddingVector embedding,
        string embeddingModelId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(embedding);
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(embeddingModelId);

        return new DocumentChunk(
            Guid.CreateVersion7(),
            document.Id,
            document.Corpus,
            ordinal,
            text,
            embedding,
            embeddingModelId);
    }
}
