using WealthOps.Domain.Enums;

namespace WealthOps.Application.Features.Ingestion;

/// <summary>
/// Maps a file's position in the personal data tree to what kind of document it is.
/// </summary>
/// <remarks>
/// <para>
/// Routing is by folder, following the layout in BRD §7.2, because the alternative — sniffing file
/// contents — would mean an agent-facing component that reads personal material to decide what it
/// is. Folder position is a fact about the operator's filing, not about the content.
/// </para>
/// <para>
/// A file in an unrecognised position routes to <see cref="DocumentType.Unclassified"/> rather
/// than being guessed at or silently dropped: <c>ingest</c> reports the count, and the operator
/// either moves the file or the routing table grows (BR-10, NFR-7).
/// </para>
/// </remarks>
public static class PersonalDataRouter
{
    /// <summary>
    /// Classifies a path expressed relative to the personal data root.
    /// </summary>
    /// <param name="relativePath">
    /// Path relative to <c>WealthOps:PersonalDataDirectory</c>, in either separator convention.
    /// </param>
    public static DocumentType Route(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        string[] segments = relativePath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // The last segment is the file name; routing keys off the folders above it.
        if (segments.Length < 2)
        {
            return DocumentType.Unclassified;
        }

        string first = segments[0].ToLowerInvariant();
        string second = segments.Length >= 3 ? segments[1].ToLowerInvariant() : string.Empty;

        return (first, second) switch
        {
            ("transactions", "format-a") => DocumentType.TransactionExportFormatA,
            ("transactions", "format-b") => DocumentType.TransactionExportFormatB,
            ("documents", "payslips") => DocumentType.Payslip,
            ("documents", "tax-statements") => DocumentType.TaxStatement,
            ("documents", "annual-statements") => DocumentType.AnnualStatement,
            ("documents", "loan") => DocumentType.LoanSchedule,
            ("reference", _) => DocumentType.Reference,
            ("reconciliation", _) => DocumentType.Reference,
            _ => DocumentType.Unclassified
        };
    }
}
