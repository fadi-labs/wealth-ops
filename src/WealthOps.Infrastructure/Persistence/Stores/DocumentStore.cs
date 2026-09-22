using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WealthOps.Application.Common.Persistence;
using WealthOps.Domain.Entities;
using WealthOps.Domain.ValueObjects;

namespace WealthOps.Infrastructure.Persistence.Stores;

/// <summary>
/// Records documents by content hash.
/// </summary>
/// <remarks>
/// The idempotence in <see cref="RecordAsync"/> is the point of this type (BR-11, AC-1). Note the
/// third case: content already known under a different path is neither "new" nor "unchanged" — the
/// file moved. Treating it as new would duplicate; treating it as unchanged would leave a stale
/// path that no longer resolves.
/// </remarks>
internal sealed class DocumentStore(
    WealthOpsDbContext dbContext,
    ILogger<DocumentStore> logger)
    : IDocumentStore
{
    public async Task<DocumentRecordResult> RecordAsync(
        Document document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        Document? existing = await FindByContentHashAsync(document.ContentHash, cancellationToken);

        if (existing is null)
        {
            dbContext.Documents.Add(document);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogDebug("Recorded document {DocumentId} in corpus {Corpus}", document.Id, document.Corpus);

            return new DocumentRecordResult(document, DocumentRecordOutcome.Recorded);
        }

        if (string.Equals(existing.AbsolutePath, document.AbsolutePath, StringComparison.Ordinal))
        {
            logger.LogDebug("Document {DocumentId} already recorded; no change", existing.Id);

            return new DocumentRecordResult(existing, DocumentRecordOutcome.AlreadyKnown);
        }

        existing.RelocateTo(document.AbsolutePath);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Path values are personal content, so the log records that a move happened, not to where.
        logger.LogDebug("Document {DocumentId} relocated; path updated", existing.Id);

        return new DocumentRecordResult(existing, DocumentRecordOutcome.Relocated);
    }

    public Task<Document?> FindByContentHashAsync(
        ContentHash contentHash,
        CancellationToken cancellationToken = default)
        => dbContext.Documents.SingleOrDefaultAsync(d => d.ContentHash == contentHash, cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
        => dbContext.Documents.CountAsync(cancellationToken);
}
