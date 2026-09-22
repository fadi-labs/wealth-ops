using WealthOps.Domain.Entities;
using WealthOps.Domain.Enums;
using WealthOps.Domain.ValueObjects;

namespace WealthOps.Application.Common.Persistence;

/// <summary>
/// A similarity query, always against exactly one corpus.
/// </summary>
/// <remarks>
/// <para>
/// <paramref name="Corpus"/> is a required positional member, so FR-M1-3 ("retrieval is
/// partitioned by corpus; no cross-corpus similarity search") is enforced by the compiler rather
/// than by reviewer attention. There is no overload that omits it and no default that guesses.
/// </para>
/// <para>
/// <paramref name="QueryText"/> is carried even though the M0 implementation ignores it. v1
/// retrieval is dense-only, which is the right default here: queries are asked in English against
/// Danish source material (FR-M1-4), and lexical matching fails across languages. But the highest-
/// value tokens in this domain are exact strings that embeddings handle poorly — ISINs, statutory
/// references, Danish term names — so a lexical leg is likely to earn its place later. Carrying
/// the text now means adding one means changing an Infrastructure class and a migration, not an
/// Application contract that M1–M3 have already been built against. It is also what a rerank stage
/// would need.
/// </para>
/// </remarks>
/// <param name="Corpus">The partition to search. Required.</param>
/// <param name="Embedding">The query embedding, from the configured embedding model.</param>
/// <param name="QueryText">The query as the operator phrased it. Unused in v1.</param>
public sealed record CorpusQuery(Corpus Corpus, EmbeddingVector Embedding, string QueryText);

/// <summary>One hit from a similarity search.</summary>
/// <param name="ChunkId">The matching chunk.</param>
/// <param name="DocumentId">The document the chunk belongs to, for citation (BR-3).</param>
/// <param name="Ordinal">Position within the document, for citation.</param>
/// <param name="Text">The chunk text, for quotation.</param>
/// <param name="Distance">
/// Cosine distance — <c>0</c> is identical, <c>2</c> is opposite. Lower is closer. Exposed as
/// distance rather than a normalised "score" so it cannot be mistaken for a confidence.
/// </param>
public sealed record CorpusSearchResult(
    Guid ChunkId,
    Guid DocumentId,
    int Ordinal,
    string Text,
    double Distance);

/// <summary>
/// A distinct combination of embedding model and dimension present in the store.
/// </summary>
/// <remarks>
/// More than one row here means the table holds vectors that are not comparable with each other
/// (ADR-002). <c>status</c> surfaces this so it is caught before it quietly degrades retrieval.
/// </remarks>
/// <param name="EmbeddingModelId">The model that produced these vectors.</param>
/// <param name="Dimensions">Their width.</param>
/// <param name="ChunkCount">How many chunks carry this combination.</param>
public sealed record EmbeddingProfile(string EmbeddingModelId, int Dimensions, int ChunkCount);

/// <summary>
/// Stores and searches embedded chunks, partitioned by corpus.
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Inserts or replaces chunks, keyed on <c>(DocumentId, Ordinal)</c>.
    /// </summary>
    /// <remarks>
    /// Upsert rather than insert so that re-chunking a document that changed does not require the
    /// caller to sequence a delete first, and so re-running ingestion is idempotent (BR-11).
    /// </remarks>
    /// <exception cref="Exceptions.EmbeddingDimensionMismatchException">
    /// A chunk's embedding width differs from the configured model's. Rejected rather than stored:
    /// a mismatched vector returns confident-looking neighbours forever after (NFR-7, ADR-002).
    /// </exception>
    Task UpsertChunksAsync(
        IReadOnlyCollection<DocumentChunk> chunks,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the nearest chunks within the query's corpus, closest first.</summary>
    /// <param name="query">The query. Its corpus bounds the search absolutely.</param>
    /// <param name="limit">Maximum hits to return. Must be positive.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<CorpusSearchResult>> SearchAsync(
        CorpusQuery query,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Returns every distinct model/dimension combination present in the store.</summary>
    Task<IReadOnlyList<EmbeddingProfile>> GetEmbeddingProfilesAsync(CancellationToken cancellationToken = default);
}
