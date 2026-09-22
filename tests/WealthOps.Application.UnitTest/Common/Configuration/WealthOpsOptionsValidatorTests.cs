using FluentValidation.Results;
using WealthOps.Application.Common.Configuration;
using WealthOps.Domain.Enums;

namespace WealthOps.Application.UnitTest.Common.Configuration;

/// <summary>
/// Every fixture here is invented (ADR-003, BR-12). Identifiers are labels like
/// <c>taxpayer-a</c> and <c>SYNTHETIC_MUNICIPALITY</c> — nothing names a real municipality,
/// provider, or account.
/// </summary>
public sealed class WealthOpsOptionsValidatorTests
{
    private static readonly WealthOpsOptionsValidator _validator = new();

    [Fact]
    public void AValidHouseholdPasses()
    {
        ValidationResult result = _validator.Validate(SyntheticOptions());

        result.IsValid.ShouldBeTrue(result.ToString());
    }

    [Fact]
    public void ATaxpayerWithNoIncomeOrAssetsIsValid()
    {
        // BR-5 requires the model to tolerate this rather than assume against it: a spouse with
        // no income and no accounts is a supported household, not a degenerate one.
        WealthOpsOptions options = MarriedCoupleOptions();
        options.Accounts = [SyntheticAccount("account-1", "taxpayer-a")];

        _validator.Validate(options).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void APersonalDataDirectoryMustBeAbsolute()
    {
        WealthOpsOptions options = SyntheticOptions();
        options.PersonalDataDirectory = "relative/personal";

        ShouldFail(options);
    }

    [Fact]
    public void APersonalDataDirectoryMustBeSet()
    {
        WealthOpsOptions options = SyntheticOptions();
        options.PersonalDataDirectory = string.Empty;

        ShouldFail(options);
    }

    [Fact]
    public void TheRulesCacheMustNotSitInsideThePersonalRoot()
    {
        // Nesting them would put agent-readable public content behind a denied path and treat
        // personal content as if it were public. Two roots is the enforcement mechanism.
        WealthOpsOptions options = SyntheticOptions();
        options.RulesCacheDirectory = Path.Combine(options.PersonalDataDirectory, "rules");

        ShouldFail(options);
    }

    [Fact]
    public void AnAccountMustReferenceAConfiguredTaxpayer()
    {
        WealthOpsOptions options = SyntheticOptions();
        options.Accounts = [SyntheticAccount("account-1", ownerTaxpayerId: "taxpayer-who-does-not-exist")];

        ShouldFail(options);
    }

    [Fact]
    public void AnAccountMustNameAnOwner()
    {
        WealthOpsOptions options = SyntheticOptions();
        options.Accounts = [SyntheticAccount("account-1", ownerTaxpayerId: string.Empty)];

        ShouldFail(options);
    }

    [Fact]
    public void AMarriedTaxpayerMustNameASpouse()
    {
        WealthOpsOptions options = SyntheticOptions();
        options.Taxation.Household.Taxpayers[0].MaritalStatus = MaritalStatus.Married;
        options.Taxation.Household.Taxpayers[0].SpouseId = null;

        ShouldFail(options);
    }

    [Fact]
    public void ASpouseReferenceMustResolveToAConfiguredTaxpayer()
    {
        WealthOpsOptions options = SyntheticOptions();
        options.Taxation.Household.Taxpayers[0].MaritalStatus = MaritalStatus.Married;
        options.Taxation.Household.Taxpayers[0].SpouseId = "taxpayer-who-does-not-exist";

        ShouldFail(options);
    }

    [Fact]
    public void ASpouseReferenceMustBeMutual()
    {
        // A one-directional link would make personfradrag transfer (BR-5) depend on which
        // taxpayer happened to be assessed first.
        WealthOpsOptions options = MarriedCoupleOptions();
        options.Taxation.Household.Taxpayers[1].SpouseId = null;
        options.Taxation.Household.Taxpayers[1].MaritalStatus = MaritalStatus.Single;

        ShouldFail(options);
    }

    [Fact]
    public void ATaxpayerCannotBeTheirOwnSpouse()
    {
        WealthOpsOptions options = SyntheticOptions();
        options.Taxation.Household.Taxpayers[0].MaritalStatus = MaritalStatus.Married;
        options.Taxation.Household.Taxpayers[0].SpouseId = "taxpayer-a";

        ShouldFail(options);
    }

    [Fact]
    public void ASingleTaxpayerCannotNameASpouse()
    {
        WealthOpsOptions options = MarriedCoupleOptions();
        options.Taxation.Household.Taxpayers[0].MaritalStatus = MaritalStatus.Single;

        ShouldFail(options);
    }

    [Fact]
    public void ATaxpayerMustNameAMunicipality()
    {
        WealthOpsOptions options = SyntheticOptions();
        options.Taxation.Household.Taxpayers[0].Municipality = string.Empty;

        ShouldFail(options);
    }

    [Fact]
    public void TaxpayerIdentifiersMustBeUnique()
    {
        WealthOpsOptions options = SyntheticOptions();
        options.Taxation.Household.Taxpayers =
        [
            SyntheticTaxpayer("taxpayer-a"),
            SyntheticTaxpayer("taxpayer-a")
        ];

        ShouldFail(options);
    }

    [Fact]
    public void AccountIdentifiersMustBeUnique()
    {
        WealthOpsOptions options = SyntheticOptions();
        options.Accounts =
        [
            SyntheticAccount("account-1", "taxpayer-a"),
            SyntheticAccount("account-1", "taxpayer-a")
        ];

        ShouldFail(options);
    }

    [Fact]
    public void AtLeastOneTaxpayerIsRequired()
    {
        WealthOpsOptions options = SyntheticOptions();
        options.Taxation.Household.Taxpayers = [];
        options.Accounts = [];

        ShouldFail(options);
    }

    [Fact]
    public void AtLeastOneTaxYearIsRequired()
    {
        WealthOpsOptions options = SyntheticOptions();
        options.Taxation.TaxYears = [];

        ShouldFail(options);
    }

    [Fact]
    public void TaxYearsMustBePlausible()
    {
        WealthOpsOptions options = SyntheticOptions();
        options.Taxation.TaxYears = [26];

        ShouldFail(options);
    }

    [Fact]
    public void TaxYearsMustNotRepeat()
    {
        WealthOpsOptions options = SyntheticOptions();
        options.Taxation.TaxYears = [2026, 2026];

        ShouldFail(options);
    }

    [Fact]
    public void TheInstrumentTableMustBeConfigured()
    {
        WealthOpsOptions options = SyntheticOptions();
        options.Instruments = string.Empty;

        ShouldFail(options);
    }

    private static void ShouldFail(WealthOpsOptions options)
    {
        ValidationResult result = _validator.Validate(options);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }

    private static WealthOpsOptions SyntheticOptions() => new()
    {
        PersonalDataDirectory = SyntheticRoot("personal"),
        RulesCacheDirectory = SyntheticRoot("rules-cache"),
        Instruments = Path.Combine(SyntheticRoot("personal"), "reference", "instruments.csv"),
        Taxation = new TaxationOptions
        {
            TaxYears = [2025, 2026],
            Household = new HouseholdOptions
            {
                Taxpayers = [SyntheticTaxpayer("taxpayer-a")]
            }
        },
        Accounts = [SyntheticAccount("account-1", "taxpayer-a")]
    };

    private static WealthOpsOptions MarriedCoupleOptions()
    {
        WealthOpsOptions options = SyntheticOptions();

        options.Taxation.Household.Taxpayers =
        [
            SyntheticTaxpayer("taxpayer-a", MaritalStatus.Married, "taxpayer-b"),
            SyntheticTaxpayer("taxpayer-b", MaritalStatus.Married, "taxpayer-a")
        ];

        return options;
    }

    private static TaxpayerOptions SyntheticTaxpayer(
        string id,
        MaritalStatus maritalStatus = MaritalStatus.Single,
        string? spouseId = null) => new()
        {
            Id = id,
            Municipality = "SYNTHETIC_MUNICIPALITY",
            ChurchTaxMember = false,
            MaritalStatus = maritalStatus,
            SpouseId = spouseId
        };

    private static AccountOptions SyntheticAccount(string id, string ownerTaxpayerId) => new()
    {
        Id = id,
        Wrapper = AccountWrapper.FrieMidler,
        ExportFormat = TransactionExportFormat.FormatA,
        OwnerTaxpayerId = ownerTaxpayerId
    };

    private static string SyntheticRoot(string leaf)
        => Path.Combine(Path.GetTempPath(), "wealthops-synthetic", leaf);
}
