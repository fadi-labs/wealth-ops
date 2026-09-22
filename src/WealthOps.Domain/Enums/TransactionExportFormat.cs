namespace WealthOps.Domain.Enums;

/// <summary>
/// The technical shape of a transaction export, identified by structure rather than by provider.
/// </summary>
/// <remarks>
/// Naming these by shape rather than by the institution that emits them is deliberate (BR-16):
/// which account exports which format is configuration, and a second provider emitting the same
/// shape needs no new member. Parser selection keys off this value (EP-1).
/// </remarks>
public enum TransactionExportFormat
{
    /// <summary>Tab-delimited, 30 named columns, ISO dates, signed amounts.</summary>
    FormatA = 1,

    /// <summary>Semicolon-delimited, 26 named columns plus a trailing empty field, dd-MM-yyyy dates, unsigned amounts.</summary>
    FormatB = 2
}
