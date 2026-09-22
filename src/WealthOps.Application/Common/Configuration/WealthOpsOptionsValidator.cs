using FluentValidation;
using WealthOps.Domain.Enums;
using WealthOps.Domain.ValueObjects;

namespace WealthOps.Application.Common.Configuration;

/// <summary>
/// Validates the operator's configuration at startup.
/// </summary>
/// <remarks>
/// <para>
/// This class is where the household rules are actually stated. Documentation cannot enforce that
/// an account references a configured taxpayer or that a spouse link is mutual; a validator can,
/// and it fails at startup rather than at the moment a tax figure comes out wrong (ADR-003).
/// </para>
/// <para>
/// Directory <em>existence</em> is deliberately not checked here — see
/// <see cref="WealthOpsOptions.PersonalDataDirectory"/>. Shape is a startup concern; a missing
/// directory is something <c>status</c> reports.
/// </para>
/// </remarks>
public sealed class WealthOpsOptionsValidator : AbstractValidator<WealthOpsOptions>
{
    public WealthOpsOptionsValidator()
    {
        RuleFor(o => o.PersonalDataDirectory)
            .NotEmpty()
            .WithMessage("WealthOps:PersonalDataDirectory must be set, or WEALTHOPS_PERSONAL_DATA_DIR must be exported.")
            .Must(BeAnAbsolutePath)
            .WithMessage("WealthOps:PersonalDataDirectory must be an absolute path outside the repository.");

        RuleFor(o => o.RulesCacheDirectory)
            .NotEmpty()
            .WithMessage("WealthOps:RulesCacheDirectory must be set.")
            .Must(BeAnAbsolutePath)
            .WithMessage("WealthOps:RulesCacheDirectory must be an absolute path.")
            .Must((options, rulesCache) => !SharesRootWith(rulesCache, options.PersonalDataDirectory))
            .WithMessage(
                "WealthOps:RulesCacheDirectory must not sit inside WealthOps:PersonalDataDirectory. " +
                "The rules cache is public and agent-readable; nesting it inside the personal root " +
                "would put agent-readable content behind a denied path and personal content at risk " +
                "of being treated as public.");

        RuleFor(o => o.Instruments)
            .NotEmpty()
            .WithMessage("WealthOps:Instruments must point at the instrument classification table.");

        RuleFor(o => o.Taxation).NotNull().SetValidator(new TaxationOptionsValidator());

        RuleForEach(o => o.Accounts).SetValidator(new AccountOptionsValidator());

        RuleFor(o => o.Accounts)
            .Must(HaveUniqueIds)
            .WithMessage("WealthOps:Accounts contains duplicate identifiers.");

        RuleFor(o => o)
            .Must(AccountsReferenceConfiguredTaxpayers)
            .WithMessage(
                "Every WealthOps:Accounts entry must set OwnerTaxpayerId to a configured " +
                "WealthOps:Taxation:Household:Taxpayers identifier.")
            .Must(SpouseReferencesResolveAndAreMutual)
            .WithMessage(
                "A married taxpayer must name a SpouseId that resolves to another configured " +
                "taxpayer, and that taxpayer must name them back.");
    }

    private static bool BeAnAbsolutePath(string path)
        => !string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path);

    private static bool SharesRootWith(string candidate, string root)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        if (!Path.IsPathRooted(candidate) || !Path.IsPathRooted(root))
        {
            return false;
        }

        string normalisedCandidate = Normalise(candidate);
        string normalisedRoot = Normalise(root);

        return normalisedCandidate.StartsWith(normalisedRoot, StringComparison.OrdinalIgnoreCase);

        static string Normalise(string path)
            => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) + Path.DirectorySeparatorChar;
    }

    private static bool HaveUniqueIds(IList<AccountOptions> accounts)
        => accounts.Select(a => a.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == accounts.Count(a => !string.IsNullOrWhiteSpace(a.Id));

    private static bool AccountsReferenceConfiguredTaxpayers(WealthOpsOptions options)
    {
        HashSet<string> taxpayerIds = TaxpayerIds(options);

        return options.Accounts.All(account =>
            !string.IsNullOrWhiteSpace(account.OwnerTaxpayerId)
            && taxpayerIds.Contains(account.OwnerTaxpayerId));
    }

    private static bool SpouseReferencesResolveAndAreMutual(WealthOpsOptions options)
    {
        IList<TaxpayerOptions> taxpayers = options.Taxation.Household.Taxpayers;

        Dictionary<string, TaxpayerOptions> byId = taxpayers
            .Where(t => !string.IsNullOrWhiteSpace(t.Id))
            .GroupBy(t => t.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (TaxpayerOptions taxpayer in taxpayers)
        {
            if (taxpayer.MaritalStatus != MaritalStatus.Married)
            {
                // A single taxpayer naming a spouse is a contradiction, not a harmless extra.
                if (!string.IsNullOrWhiteSpace(taxpayer.SpouseId))
                {
                    return false;
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(taxpayer.SpouseId)
                || string.Equals(taxpayer.SpouseId, taxpayer.Id, StringComparison.OrdinalIgnoreCase)
                || !byId.TryGetValue(taxpayer.SpouseId, out TaxpayerOptions? spouse))
            {
                return false;
            }

            bool mutual = spouse.MaritalStatus == MaritalStatus.Married
                && string.Equals(spouse.SpouseId, taxpayer.Id, StringComparison.OrdinalIgnoreCase);

            if (!mutual)
            {
                return false;
            }
        }

        return true;
    }

    private static HashSet<string> TaxpayerIds(WealthOpsOptions options)
        => options.Taxation.Household.Taxpayers
            .Select(t => t.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}

internal sealed class TaxationOptionsValidator : AbstractValidator<TaxationOptions>
{
    public TaxationOptionsValidator()
    {
        RuleFor(t => t.TaxYears)
            .NotEmpty()
            .WithMessage("WealthOps:Taxation:TaxYears must list at least one tax year.")
            .Must(years => years.All(y => TaxYear.TryCreate(y, out _)))
            .WithMessage("WealthOps:Taxation:TaxYears contains a value that is not a plausible tax year.")
            .Must(years => years.Distinct().Count() == years.Count)
            .WithMessage("WealthOps:Taxation:TaxYears contains duplicates.");

        RuleFor(t => t.Household).NotNull().SetValidator(new HouseholdOptionsValidator());
    }
}

internal sealed class HouseholdOptionsValidator : AbstractValidator<HouseholdOptions>
{
    public HouseholdOptionsValidator()
    {
        RuleFor(h => h.Taxpayers)
            .NotEmpty()
            .WithMessage("WealthOps:Taxation:Household:Taxpayers must list at least one taxpayer.")
            .Must(HaveUniqueIds)
            .WithMessage("WealthOps:Taxation:Household:Taxpayers contains duplicate identifiers.");

        RuleForEach(h => h.Taxpayers).SetValidator(new TaxpayerOptionsValidator());
    }

    private static bool HaveUniqueIds(IList<TaxpayerOptions> taxpayers)
        => taxpayers.Select(t => t.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == taxpayers.Count(t => !string.IsNullOrWhiteSpace(t.Id));
}

internal sealed class TaxpayerOptionsValidator : AbstractValidator<TaxpayerOptions>
{
    public TaxpayerOptionsValidator()
    {
        RuleFor(t => t.Id)
            .NotEmpty()
            .WithMessage("Each taxpayer must carry an identifier that accounts and documents can reference.");

        RuleFor(t => t.Municipality)
            .NotEmpty()
            .WithMessage("Each taxpayer must name the municipality whose rate applies.");

        RuleFor(t => t.MaritalStatus)
            .IsInEnum()
            .WithMessage("Each taxpayer must have a recognised marital status.");
    }
}

internal sealed class AccountOptionsValidator : AbstractValidator<AccountOptions>
{
    public AccountOptionsValidator()
    {
        RuleFor(a => a.Id)
            .NotEmpty()
            .WithMessage("Each account must carry an operator-chosen identifier.");

        RuleFor(a => a.Wrapper)
            .IsInEnum()
            .WithMessage("Each account must declare a recognised wrapper; the wrapper decides how it is taxed.");

        RuleFor(a => a.ExportFormat)
            .IsInEnum()
            .WithMessage("Each account must declare a recognised export format; the format selects the parser.");

        RuleFor(a => a.OwnerTaxpayerId)
            .NotEmpty()
            .WithMessage("Each account must name its owning taxpayer.");
    }
}
