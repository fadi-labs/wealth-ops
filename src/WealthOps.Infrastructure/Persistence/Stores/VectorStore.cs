using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using WealthOps.Application.Common.Clients;
using WealthOps.Application.Common.Exceptions;
using WealthOps.Application.Common.Persistence;
using WealthOps.Domain.Entities;

namespace WealthOps.Infrastructure.Persistence.Stores;

/// <summary>
/// Stores and searches embedded chunks, always within one corpus.
/// </summary>
internal sealed class VectorStore(
    WealthOpsDbContext dbContext,
    IEmbeddingModel embeddingModel,
    ILogger<VectorStore> logger)
    : IVectorStore
{
    public async Task UpsertChunksAsync(
        IReadOnlyCollection<DocumentChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        if (chunks.Count == 0)
        {
            return;
        }

        // Checked before anything is written, so a bad batch fails whole rather than half
        // (ADR-002). A vector of the wrong width is unrecoverable once it is in the table.
        foreach (DocumentChunk chunk in chunks)
        {
            if (chunk.Dimensions != embeddingModel.Dimensions)
            {
                throw new EmbeddingDimensionMismatchException(
                    chunk.EmbeddingModelId,
                    embeddingModel.Dimensions,
                    chunk.Dimensions);
            }
        }

        Guid[] documentIds = [.. chunks.Select(c => c.DocumentId).Distinct()];
        int[] ordinals = [.. chunks.Select(c => c.Ordinal).Distinct()];

        List<DocumentChunk> existing = await dbContext.DocumentChunks
            .Where(c => documentIds.Contains(c.DocumentId) && ordinals.Contains(c.Ordinal))
            .ToListAsync(cancellationToken);

        Dictionary<(Guid DocumentId, int Ordinal), DocumentChunk> existingByKey =
            existing.ToDictionary(c => (c.DocumentId, c.Ordinal));

        foreach (DocumentChunk chunk in chunks)
        {
            if (existingByKey.TryGetValue((chunk.DocumentId, chunk.Ordinal), out DocumentChunk? previous))
            {
                dbContext.DocumentChunks.Remove(previous);
            }

            dbContext.DocumentChunks.Add(chunk);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogDebug("Upserted {ChunkCount} chunks across {DocumentCount} documents", chunks.Count, documentIds.Length);
    }

    public async Task<IReadOnlyList<CorpusSearchResult>> SearchAsync(
        CorpusQuery query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        if (query.Embedding.Dimensions != embeddingModel.Dimensions)
        {
            throw new EmbeddingDimensionMismatchException(
                embeddingModel.ModelId,
                embeddingModel.Dimensions,
                query.Embedding.Dimensions);
        }

        var target = new Vector(query.Embedding.ToArray());

        // The corpus filter is applied in the same statement as the ordering, so there is no
        // arrangement of this query that can return a chunk from another corpus (FR-M1-3).
        // v1 is dense-only; query.QueryText is carried for a future lexical leg (see CorpusQuery).
        List<CorpusSearchResult> results = await dbContext.DocumentChunks
            .Where(c => c.Corpus == query.Corpus)
            .OrderBy(c => c.Embedding.CosineDistance(target))
            .Take(limit)
            .Select(c => new CorpusSearchResult(
                c.Id,
                c.DocumentId,
                c.Ordinal,
                c.Text,
                c.Embedding.CosineDistance(target)))
            .ToListAsync(cancellationToken);

        logger.LogDebug("Corpus {Corpus} search returned {ResultCount} chunks", query.Corpus, results.Count);

        return results;
    }

    public async Task<IReadOnlyList<EmbeddingProfile>> GetEmbeddingProfilesAsync(
        CancellationToken cancellationToken = default)
    {
        List<EmbeddingProfile> profiles = await dbContext.DocumentChunks
            .GroupBy(c => new { c.EmbeddingModelId, c.Dimensions })
            .Select(g => new EmbeddingProfile(g.Key.EmbeddingModelId, g.Key.Dimensions, g.Count()))
            .ToListAsync(cancellationToken);

        return profiles;
    }
}
