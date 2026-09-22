using WealthOps.Domain.Enums;

namespace WealthOps.Application.Common.Configuration;

/// <summary>
/// The operator's situation, in full.
/// </summary>
/// <remarks>
/// <para>
/// Every particular of the household, its accounts and its data locations lives here and nowhere
/// else (BR-16, ADR-003). Nothing in this file names a municipality, a provider, an account or a
/// person — it names the <em>shape</em> of those facts. The committed
/// <c>appsettings.Example.json</c> carries placeholders; real values live in a gitignored
/// <c>appsettings.Local.json</c> or in <c>WEALTHOPS_*</c> environment variables.
/// </para>
/// <para>
/// Validated at startup by <see cref="WealthOpsOptionsValidator"/>.
/// </para>
/// </remarks>
public sealed class WealthOpsOptions
{
    public const string SectionName = "WealthOps";

    /// <summary>
    /// Root directory for the operator's source documents and exports.
    /// </summary>
    /// <remarks>
    /// Lives outside the repository by construction. Supplied by
    /// <c>WEALTHOPS_PERSONAL_DATA_DIR</c> or this key, defaulting to <c>~/.wealthops/personal</c>.
    /// Its existence is reported by <c>status</c> rather than enforced at startup, so a mistyped
    /// path can be diagnosed instead of merely preventing boot.
    /// </remarks>
    public string PersonalDataDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Cache directory for the public rules corpus.
    /// </summary>
    /// <remarks>
    /// Deliberately a separate root from <see cref="PersonalDataDirectory"/>. Rules content is
    /// public and agent-readable so retrieval quality can be debugged against it; personal content
    /// is neither. Two roots are what make that distinction enforceable rather than procedural.
    /// </remarks>
    public string RulesCacheDirectory { get; set; } = string.Empty;

    /// <summary>Path to the operator-maintained ISIN classification table (EP-7).</summary>
    public string Instruments { get; set; } = string.Empty;

    public TaxationOptions Taxation { get; set; } = new();

    public IList<AccountOptions> Accounts { get; set; } = [];
}

/// <summary>Household composition and the years under assessment.</summary>
public sealed class TaxationOptions
{
    public HouseholdOptions Household { get; set; } = new();

    /// <summary>
    /// The tax years in scope.
    /// </summary>
    /// <remarks>
    /// More than one is the normal case, not an edge case: bracket structures differ between
    /// adjacent years, so the system must hold both concurrently (BR-4).
    /// </remarks>
    public IList<int> TaxYears { get; set; } = [];
}

/// <summary>The taxpayers assessed together.</summary>
public sealed class HouseholdOptions
{
    public IList<TaxpayerOptions> Taxpayers { get; set; } = [];
}

/// <summary>
/// One taxpayer.
/// </summary>
/// <remarks>
/// A taxpayer with no income and no assets is a supported configuration, not a degenerate one
/// (BR-5) — nothing here may assume otherwise.
/// </remarks>
public sealed class TaxpayerOptions
{
    /// <summary>
    /// Stable identifier used to reference this taxpayer from accounts and documents.
    /// </summary>
    /// <remarks>An operator-chosen label, never a name or a national identifier.</remarks>
    public string Id { get; set; } = string.Empty;

    /// <summary>The municipality whose rate applies. An identifier resolved to a rate in M3 (OI-1).</summary>
    public string Municipality { get; set; } = string.Empty;

    /// <summary>Whether church tax applies to this taxpayer.</summary>
    public bool ChurchTaxMember { get; set; }

    public MaritalStatus MaritalStatus { get; set; } = MaritalStatus.Single;

    /// <summary>
    /// The <see cref="Id"/> of the spouse, when <see cref="MaritalStatus"/> is
    /// <see cref="MaritalStatus.Married"/>.
    /// </summary>
    /// <remarks>
    /// Required, resolvable and mutual when married — the validator enforces all three. A
    /// one-directional spouse link would make personfradrag transfer (BR-5) compute differently
    /// depending on which taxpayer was assessed first.
    /// </remarks>
    public string? SpouseId { get; set; }
}

/// <summary>One account, identified by its wrapper and export shape rather than its provider.</summary>
public sealed class AccountOptions
{
    /// <summary>Operator-chosen identifier, never the real account or depot number.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Which tax regime applies to the account as a whole.</summary>
    public AccountWrapper Wrapper { get; set; }

    /// <summary>Which parser handles this account's exports (EP-1).</summary>
    public TransactionExportFormat ExportFormat { get; set; }

    /// <summary>The <see cref="TaxpayerOptions.Id"/> that owns this account.</summary>
    public string OwnerTaxpayerId { get; set; } = string.Empty;
}
