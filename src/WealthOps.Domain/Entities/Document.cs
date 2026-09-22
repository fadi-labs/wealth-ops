using WealthOps.Domain.Enums;
using WealthOps.Domain.ValueObjects;

namespace WealthOps.Domain.Entities;

/// <summary>
/// A source file that has been ingested, identified by the hash of its content.
/// </summary>
/// <remarks>
/// The record exists so that re-ingesting an unchanged file is a no-op (BR-11, AC-1) and so that
/// every chunk, and eventually every computed figure, can be traced back to the file it came from
/// (NFR-5). In M0 that is all it does — text extraction, chunking and embedding arrive in M1.
/// </remarks>
public sealed class Document
{
    private Document(
        Guid id,
        string absolutePath,
        DocumentType documentType,
        ContentHash contentHash,
        Corpus corpus,
        string? taxpayerId,
        TaxYear? taxYear,
        DateTimeOffset ingestedAtUtc)
    {
        Id = id;
        AbsolutePath = absolutePath;
        DocumentType = documentType;
        ContentHash = contentHash;
        Corpus = corpus;
        TaxpayerId = taxpayerId;
        TaxYear = taxYear;
        IngestedAtUtc = ingestedAtUtc;
    }

    /// <summary>Required by EF Core materialisation. Not for application use.</summary>
    private Document()
    {
        AbsolutePath = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>Absolute path to the source file on the local machine.</summary>
    public string AbsolutePath { get; private set; }

    public DocumentType DocumentType { get; private set; }

    public ContentHash ContentHash { get; private set; }

    public Corpus Corpus { get; private set; }

    /// <summary>
    /// The configured taxpayer this document belongs to, where that is known at ingest time.
    /// An identifier from configuration — never a name (BR-16).
    /// </summary>
    public string? TaxpayerId { get; private set; }

    /// <summary>The tax year this document pertains to, where the routing makes that unambiguous.</summary>
    public TaxYear? TaxYear { get; private set; }

    public DateTimeOffset IngestedAtUtc { get; private set; }

    /// <exception cref="ArgumentException">
    /// The path is blank or not rooted, or a taxpayer identifier is present but blank.
    /// </exception>
    public static Document Create(
        string absolutePath,
        DocumentType documentType,
        ContentHash contentHash,
        Corpus corpus,
        DateTimeOffset ingestedAtUtc,
        string? taxpayerId = null,
        TaxYear? taxYear = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);

        if (!Path.IsPathRooted(absolutePath))
        {
            throw new ArgumentException(
                "Document path must be absolute so the source file stays resolvable regardless of working directory.",
                nameof(absolutePath));
        }

        if (taxpayerId is not null && string.IsNullOrWhiteSpace(taxpayerId))
        {
            throw new ArgumentException(
                "Taxpayer identifier must be either absent or non-blank.",
                nameof(taxpayerId));
        }

        return new Document(
            Guid.CreateVersion7(),
            absolutePath,
            documentType,
            contentHash,
            corpus,
            taxpayerId,
            taxYear,
            ingestedAtUtc);
    }

    /// <summary>
    /// Records that the same content has been seen again at a different path.
    /// </summary>
    /// <remarks>
    /// Content identity wins over location (see <see cref="ValueObjects.ContentHash"/>), so a file
    /// that moved is the same document with a new path — not a second document, and not a conflict.
    /// </remarks>
    public void RelocateTo(string absolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);

        if (!Path.IsPathRooted(absolutePath))
        {
            throw new ArgumentException("Document path must be absolute.", nameof(absolutePath));
        }

        AbsolutePath = absolutePath;
    }
}
