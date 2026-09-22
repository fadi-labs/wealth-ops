namespace WealthOps.Domain.Enums;

/// <summary>
/// What kind of source document was ingested, in capability terms.
/// </summary>
/// <remarks>
/// These are categories of document, never providers. Which account produces which export, and
/// which institution issued which statement, is configuration (BR-16) — the domain only knows
/// that a file is, say, a transaction export in <c>FormatA</c> shape.
/// </remarks>
public enum DocumentType
{
    /// <summary>A tax-rule page from the public rules corpus.</summary>
    TaxRulePage = 1,

    /// <summary>A transaction export in the Format A shape (tab-delimited).</summary>
    TransactionExportFormatA = 2,

    /// <summary>A transaction export in the Format B shape (semicolon-delimited).</summary>
    TransactionExportFormatB = 3,

    /// <summary>A payslip. Retrieval and quotation only — no figures are extracted (OOS-3).</summary>
    Payslip = 4,

    /// <summary>An annual or preliminary tax statement. Also the reconciliation target (BR-6).</summary>
    TaxStatement = 5,

    /// <summary>An annual account statement — the v1 source of year-boundary market values (DS-17).</summary>
    AnnualStatement = 6,

    /// <summary>A loan amortisation schedule.</summary>
    LoanSchedule = 7,

    /// <summary>An operator-maintained reference table (instrument classification, valuations, annual facts).</summary>
    Reference = 8,

    /// <summary>Recorded but unclassified. Reported by <c>status</c> rather than guessed at (BR-10, NFR-7).</summary>
    Unclassified = 99
}
