using WealthOps.Domain.Entities;
using WealthOps.Domain.ValueObjects;

namespace WealthOps.Application.Common.Persistence;

/// <summary>What recording a document actually did.</summary>
/// <param name="Document">The stored document — the existing one when the content was already known.</param>
/// <param name="Outcome">Whether the content was new.</param>
public sealed record DocumentRecordResult(Document Document, DocumentRecordOutcome Outcome);

/// <summary>The three things that can happen when a file is offered to the store.</summary>
public enum DocumentRecordOutcome
{
    /// <summary>Content not seen before. A new document was stored.</summary>
    Recorded = 1,

    /// <summary>Content already stored at the same path. Nothing changed (AC-1).</summary>
    AlreadyKnown = 2,

    /// <summary>
    /// Content already stored, but under a different path — the file moved or was re-downloaded.
    /// The existing document's path was updated; no second document was created.
    /// </summary>
    Relocated = 3
}

/// <summary>
/// Records ingested files by content hash, which is what makes ingestion idempotent.
/// </summary>
/// <remarks>
/// BR-11 and AC-1 require that re-ingesting an unchanged export produce no change in stored data.
/// Making the content hash the identity — rather than the path, or a caller-supplied id — means
/// the operator never has to track what has already been loaded (BRD §7.2).
/// </remarks>
public interface IDocumentStore
{
    /// <summary>
    /// Records a document unless its content is already known, reporting which happened.
    /// </summary>
    Task<DocumentRecordResult> RecordAsync(Document document, CancellationToken cancellationToken = default);

    /// <summary>Finds a document by content hash, or <see langword="null"/>.</summary>
    Task<Document?> FindByContentHashAsync(ContentHash contentHash, CancellationToken cancellationToken = default);

    /// <summary>Counts stored documents, for <c>status</c>.</summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
